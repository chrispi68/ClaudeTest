namespace ClaudeTest;

public class WärmepumpeService
{
    private readonly WärmepumpeKonfig _konfig;

    public WärmepumpeService(WärmepumpeKonfig konfig)
    {
        _konfig = konfig;
    }

    /// <summary>
    /// Berechnet den stündlichen Verbrauch der Wärmepumpe basierend auf der Außentemperatur.
    /// Ab 16°C max. 0,3 kWh/h (nur Warmwasser). Nichtlineares Modell (logistische Kurve)
    /// für Temperaturen darunter, max. 2,1 kWh/h.
    /// </summary>
    public static double BerechneStündlichenVerbrauch(double aussentemperatur)
    {
        if (aussentemperatur >= 16.0)
            return 0.3;

        const double L  = 7.2;                // kWh/Tag – untere Basis (entspricht 0,3 kWh/h)
        const double U  = 33.161217548309;    // obere Sättigung (kWh/Tag)
        const double s  = 0.774892950952934;  // Steilheit
        const double Tm = 5.54977578521401;   // Mittelpunkt (°C)

        double daily  = L + (U - L) / (1.0 + Math.Exp(s * (aussentemperatur - Tm)));
        double hourly = daily / 24.0;

        return Math.Min(2.1, hourly);
    }

    /// <summary>
    /// Erstellt ein Dictionary (Stundenindex → kW) für 72 Stunden.
    /// Verwendet die prognostizierten Außentemperaturen, falls vorhanden,
    /// sonst die aktuelle Außentemperatur als Fallback.
    /// </summary>
    public Dictionary<int, double> BerechnePrognose(Dictionary<int, double>? temperaturPrognose = null)
    {
        return Enumerable.Range(0, 72).ToDictionary(
            i => i,
            i =>
            {
                double temp = temperaturPrognose?.GetValueOrDefault(i, _konfig.Aussentemperatur)
                              ?? _konfig.Aussentemperatur;
                return BerechneStündlichenVerbrauch(temp);
            });
    }
}
