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
    /// Bei >= 19°C nur Warmwasserbedarf (4 kWh/Tag gleichmäßig verteilt).
    /// Max. 2,1 kW pro Stunde.
    /// </summary>
    public static double BerechneStündlichenVerbrauch(double aussentemperatur)
    {
        double daily = aussentemperatur >= 19.0
            ? 4.0
            : 4.0 + 25.54 * Math.Exp(-0.0309 * aussentemperatur);

        return Math.Min(2.1, daily / 24.0);
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
