namespace ClaudeTest;

public class HausverbrauchService
{
    private readonly double _latitude;
    private readonly double _longitude;
    private readonly bool _istJemandZuhause;

    public HausverbrauchService(double latitude, double longitude, bool istJemandZuhause)
    {
        _latitude = latitude;
        _longitude = longitude;
        _istJemandZuhause = istJemandZuhause;
    }

    /// <summary>
    /// Berechnet den Sonnenaufgang und Sonnenuntergang für einen bestimmten Tag.
    /// Gibt lokale Uhrzeiten zurück.
    /// </summary>
    private (TimeOnly Aufgang, TimeOnly Untergang) BerechneSonnenzeiten(DateOnly datum)
    {
        // NOAA-Algorithmus (Spencer/Duffie)
        int tag = datum.DayOfYear;
        double b = 2 * Math.PI * (tag - 1) / 365.0;

        // Deklination der Sonne in Radiant
        double dekl = 0.006918
            - 0.399912 * Math.Cos(b)
            + 0.070257 * Math.Sin(b)
            - 0.006758 * Math.Cos(2 * b)
            + 0.000907 * Math.Sin(2 * b)
            - 0.002697 * Math.Cos(3 * b)
            + 0.00148  * Math.Sin(3 * b);

        double lat = _latitude * Math.PI / 180.0;

        // Stundenwinkel beim Sonnenauf/-untergang (cos(ha) = -tan(lat)*tan(dekl))
        double cosHa = -Math.Tan(lat) * Math.Tan(dekl);

        if (cosHa < -1) // Polartag
            return (new TimeOnly(0, 0), new TimeOnly(23, 59));
        if (cosHa > 1)  // Polarnacht
            return (new TimeOnly(12, 0), new TimeOnly(12, 0));

        double ha = Math.Acos(cosHa) * 180.0 / Math.PI; // Stunden-Winkel in Grad

        // Zeitgleichung (Equation of Time) in Minuten
        double eot = 229.18 * (0.000075
            + 0.001868 * Math.Cos(b)
            - 0.032077 * Math.Sin(b)
            - 0.014615 * Math.Cos(2 * b)
            - 0.04089  * Math.Sin(2 * b));

        // UTC-Offset aus lokaler Zeitzone
        double utcOffsetStunden = TimeZoneInfo.Local.GetUtcOffset(datum.ToDateTime(TimeOnly.MinValue)).TotalHours;

        // Sonnenaufgang / -untergang in Dezimalstunden (UTC + lokaler Offset)
        double aufgangUtc  = 12.0 - ha / 15.0 - _longitude / 15.0 - eot / 60.0;
        double untergangUtc = 12.0 + ha / 15.0 - _longitude / 15.0 - eot / 60.0;

        double aufgangLokal   = aufgangUtc   + utcOffsetStunden;
        double untergangLokal = untergangUtc + utcOffsetStunden;

        return (
            DezimanstundenZuTimeOnly(aufgangLokal),
            DezimanstundenZuTimeOnly(untergangLokal)
        );
    }

    private static TimeOnly DezimanstundenZuTimeOnly(double stunden)
    {
        stunden = ((stunden % 24) + 24) % 24;
        int h = (int)stunden;
        int m = (int)Math.Round((stunden - h) * 60);
        if (m == 60) { h++; m = 0; }
        return new TimeOnly(h % 24, m);
    }

    /// <summary>
    /// Erstellt ein Dictionary (Stundenindex → kW) für 72 Stunden ab der aktuellen Stunde.
    /// </summary>
    public Dictionary<int, double> BerechnePrognose()
    {
        var result = new Dictionary<int, double>();
        DateTime startzeit = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);

        // Abwesenheitslogik: 0W ab der ersten 22:00-Stunde, wenn niemand zu Hause ist,
        // bis zur nächsten 22:00-Stunde (exklusiv).
        int abwesenheitStartIndex = -1;
        int abwesenheitEndeIndex  = -1;

        if (!_istJemandZuhause)
        {
            // Erste Stunde >= 22:00 im Prognosezeitraum finden
            for (int i = 0; i < 72; i++)
            {
                DateTime stunde = startzeit.AddHours(i);
                if (stunde.Hour >= 22)
                {
                    abwesenheitStartIndex = i;
                    break;
                }
            }

            if (abwesenheitStartIndex >= 0)
            {
                // Ende: 22:00 Uhr des Folgetages
                for (int i = abwesenheitStartIndex + 1; i < 72; i++)
                {
                    DateTime stunde = startzeit.AddHours(i);
                    if (stunde.Hour == 22)
                    {
                        abwesenheitEndeIndex = i;
                        break;
                    }
                }
            }
        }

        for (int i = 0; i < 72; i++)
        {
            DateTime stunde = startzeit.AddHours(i);

            // Abwesenheit
            if (abwesenheitStartIndex >= 0 && i >= abwesenheitStartIndex &&
                (abwesenheitEndeIndex < 0 || i < abwesenheitEndeIndex))
            {
                result[i] = 0.0;
                continue;
            }

            result[i] = BerechneStunde(stunde);
        }

        return result;
    }

    private double BerechneStunde(DateTime stunde)
    {
        int h = stunde.Hour;
        bool istWochenende = stunde.DayOfWeek == DayOfWeek.Saturday || stunde.DayOfWeek == DayOfWeek.Sunday;
        var datum = DateOnly.FromDateTime(stunde);
        var (aufgang, untergang) = BerechneSonnenzeiten(datum);
        var zeitpunkt = TimeOnly.FromDateTime(stunde);

        // Nacht: 23:00–06:59 → 0 W
        if (h is >= 23 or <= 6)
            return 0.0;

        // Wochenende 07:00–08:59 → noch schlafen → 0 W
        if (istWochenende && h is 7 or 8)
            return 0.0;

        // Weckerzeit: Werktag 07:00 / Wochenende 09:00 → Kaffeemaschine 1000 W
        if (!istWochenende && h == 7)
            return 1.0;
        if (istWochenende && h == 9)
            return 1.0;

        // Abendessen 18:00 → 1000 W
        if (h == 18)
            return 1.0;

        // Abend 17:00–22:59 (außer 18:00)
        if (h >= 17)
        {
            bool nachSonnenuntergang = zeitpunkt >= untergang;
            return nachSonnenuntergang ? 0.7 : 0.5;
        }

        // Tagsüber (nach Weckerzeit bis 16:59)
        bool nachSonnenaufgang = zeitpunkt >= aufgang;
        return nachSonnenaufgang ? 0.2 : 0.4;
    }
}
