namespace ClaudeTest;

public class EnergyService
{
    public List<EnergyData> EnergyDataList { get; private set; }

    private double _basisverbrauch;
    public double MaxBatteriekapazität { get; private set; }

    public EnergyService(
        double basisverbrauchKw,
        double maxBatteriekapazitätKw,
        List<PVPrognoseEintrag> pvPrognose,
        Dictionary<int, double> wärmepumpenverbrauch,
        Dictionary<int, double> hausverbrauch,
        double initialBatterieladungKw = 0.0,
        Dictionary<int, double>? aussentemperaturen = null)
    {
        _basisverbrauch = basisverbrauchKw;
        MaxBatteriekapazität = maxBatteriekapazitätKw;
        EnergyDataList = new List<EnergyData>();

        DateTime startzeit = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);

        // PV-Prognose als Dictionary für schnellen Zugriff per Zeitstempel
        var pvLookup = pvPrognose.ToDictionary(
            p => p.PeriodStart.ToLocalTime().DateTime,
            p => p);

        for (int i = 0; i < 72; i++)
        {
            DateTime stunde = startzeit.AddHours(i);
            pvLookup.TryGetValue(stunde, out var pv);

            EnergyDataList.Add(new EnergyData
            {
                Zeitstempel = stunde,
                Basisverbrauch = _basisverbrauch,
                PVErtrag = pv?.PVEstimate ?? 0.0,
                Wärmepumpe = wärmepumpenverbrauch.GetValueOrDefault(i, 0.0),
                Hausverbrauch = hausverbrauch.GetValueOrDefault(i, 0.0),
                Aussentemperatur = aussentemperaturen?.GetValueOrDefault(i, 0.0) ?? 0.0
            });
        }

        BerechneEnergiefluss(initialBatterieladungKw);
    }

    private void BerechneEnergiefluss(double initialBatterieladungKw)
    {
        double batteriestand = initialBatterieladungKw;

        foreach (var d in EnergyDataList)
        {
            double gesamtverbrauch = d.Basisverbrauch + d.Hausverbrauch + d.Wärmepumpe;
            double bilanz = d.PVErtrag - gesamtverbrauch;

            if (bilanz >= 0)
            {
                // Überschuss: erst Batterie laden, dann ins Netz einspeisen
                double ladekapazität = MaxBatteriekapazität - batteriestand;
                double einladen = Math.Min(bilanz, ladekapazität);
                batteriestand += einladen;
                d.Einspeisung = bilanz - einladen;
                d.Netzbezug = 0;
            }
            else
            {
                // Defizit: erst Batterie entladen, dann aus Netz beziehen
                double bedarf = -bilanz;
                double entladen = Math.Min(bedarf, batteriestand);
                batteriestand -= entladen;
                d.Netzbezug = bedarf - entladen;
                d.Einspeisung = 0;
            }

            d.Batterie = batteriestand;
        }
    }

    public void SetBasisverbrauch(double basisverbrauchKw)
    {
        _basisverbrauch = basisverbrauchKw;
        foreach (var eintrag in EnergyDataList)
            eintrag.Basisverbrauch = _basisverbrauch;
    }
}
