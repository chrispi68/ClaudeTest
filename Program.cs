using System.Text.Json;
using ClaudeTest;

// Konfiguration laden
var configJson = File.ReadAllText("appsettings.json");
var config = JsonSerializer.Deserialize<AppConfig>(configJson,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("appsettings.json konnte nicht geladen werden.");

// Daten aus Home Assistant abrufen
double initialBatterieladung = 0.0;
var pvPrognose = new List<PVPrognoseEintrag>();
var wärmepumpenverbrauch = new Dictionary<int, double>();
var aussentemperaturen = new Dictionary<int, double>();
try
{
    var haService = new HomeAssistantService(config.HomeAssistant);

    initialBatterieladung = await haService.GetBatterieladungKwhAsync();
    Console.WriteLine($"Batterieladung aus Home Assistant:  {initialBatterieladung:F2} kWh");

    pvPrognose = await haService.GetPVPrognoseAsync();
    Console.WriteLine($"PV-Prognose aus Home Assistant:     {pvPrognose.Count} Einträge geladen.");

    var wpKonfig = await haService.GetWärmepumpeKonfigAsync(config.HomeAssistant.Wärmepumpe);
    Console.WriteLine($"Wärmepumpe: {wpKonfig.Betriebsart}, Außen {wpKonfig.Aussentemperatur:F1}°C, Soll-Innen {wpKonfig.SolltemperaturInnen:F1}°C");

    var wetterPrognose = await haService.GetWetterPrognoseAsync(config.HomeAssistant.WetterSensor);
    Console.WriteLine($"Wetterprognose:                     {wetterPrognose.Count} Einträge geladen.");

    // Außentemperaturen auf Stundenindex mappen (Fallback: aktuelle Außentemperatur)
    var startzeit = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
    aussentemperaturen = wetterPrognose.ToDictionary(
        w => (int)Math.Round((w.Zeitpunkt.ToLocalTime().DateTime - startzeit).TotalHours),
        w => w.Temperatur);

    wärmepumpenverbrauch = new WärmepumpeService(wpKonfig).BerechnePrognose(aussentemperaturen);
}
catch (Exception ex)
{
    Console.WriteLine($"Warnung: HA nicht erreichbar ({ex.Message}). Starte mit Standardwerten.");
}

var hausverbrauch = new Dictionary<int, double>
{
    { 0, 0.8 }, { 1, 0.6 }, { 2, 0.5 }, { 3, 0.5 }, { 4, 0.5 },
    { 5, 0.6 }, { 6, 1.2 }, { 7, 1.8 }, { 8, 1.5 }, { 9, 1.3 },
    { 10, 1.2 }, { 11, 1.1 }, { 12, 1.3 }, { 13, 1.4 }, { 14, 1.3 },
    { 15, 1.2 }, { 16, 1.4 }, { 17, 1.8 }, { 18, 2.1 }, { 19, 2.3 },
    { 20, 2.0 }, { 21, 1.5 }, { 22, 1.0 }, { 23, 0.8 }
};

// Service initialisieren
var service = new EnergyService(
    basisverbrauchKw: 0.6,
    maxBatteriekapazitätKw: 15.0,
    pvPrognose: pvPrognose,
    wärmepumpenverbrauch: wärmepumpenverbrauch,
    hausverbrauch: hausverbrauch,
    initialBatterieladungKw: initialBatterieladung,
    aussentemperaturen: aussentemperaturen
);

var EnergyDataList = service.EnergyDataList;

Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Energieprognose – nächste 72 Stunden   |  Max. Batteriekapazität: {service.MaxBatteriekapazität,4:F1} kW                                   ║");
Console.WriteLine("╠══════╦══════════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦════════╣");
Console.WriteLine("║  Std ║ Datum/Zeit   ║ PV  (kW) ║Haus (kW) ║ WP  (kW) ║Basis(kW) ║Verb.(kW) ║Batt.(kWh)║Einsp(kW) ║Netz (kW) ║Außen°C ║");
Console.WriteLine("╠══════╬══════════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬════════╣");

var gruppenNachTag = EnergyDataList.GroupBy(d => d.Zeitstempel.Date).ToList();

foreach (var gruppe in gruppenNachTag)
{
    var stundenDesTag = gruppe.ToList();

    foreach (var d in stundenDesTag)
    {
        int i = EnergyDataList.IndexOf(d);
        string marker = i == 0 ? "►" : " ";
        double gesamtverbrauch = d.Basisverbrauch + d.Hausverbrauch + d.Wärmepumpe;
        Console.WriteLine(
            $"║ {marker}{i,3} ║ {d.Zeitstempel:dd.MM. HH:mm}  ║ {d.PVErtrag,5:F2} kW ║ {d.Hausverbrauch,5:F2} kW ║ {d.Wärmepumpe,5:F2} kW ║ {d.Basisverbrauch,5:F2} kW ║ {gesamtverbrauch,5:F2} kW ║ {d.Batterie,5:F2} kWh║ {d.Einspeisung,5:F2} kW ║ {d.Netzbezug,5:F2} kW ║{d.Aussentemperatur,5:F1}°C ║"
        );
    }

    // Tagessummen
    double sumPV          = stundenDesTag.Sum(d => d.PVErtrag);
    double sumHaus        = stundenDesTag.Sum(d => d.Hausverbrauch);
    double sumWP          = stundenDesTag.Sum(d => d.Wärmepumpe);
    double sumBasis       = stundenDesTag.Sum(d => d.Basisverbrauch);
    double sumVerbrauch   = stundenDesTag.Sum(d => d.Basisverbrauch + d.Hausverbrauch + d.Wärmepumpe);
    double sumEinspeisung = stundenDesTag.Sum(d => d.Einspeisung);
    double sumNetzbezug   = stundenDesTag.Sum(d => d.Netzbezug);

    Console.ForegroundColor = ConsoleColor.Black;
    Console.BackgroundColor = ConsoleColor.Cyan;
    Console.WriteLine(
        $"║ Σ     ║ {gruppe.Key:dd.MM.} Summe  ║ {sumPV,5:F2} kW ║ {sumHaus,5:F2} kW ║ {sumWP,5:F2} kW ║ {sumBasis,5:F2} kW ║ {sumVerbrauch,5:F2} kW ║          ║ {sumEinspeisung,5:F2} kW ║ {sumNetzbezug,5:F2} kW ║        ║"
    );
    bool letzteGruppe = gruppe.Key == gruppenNachTag.Last().Key;
    Console.ResetColor();
    if (letzteGruppe)
        Console.WriteLine("╚══════╩══════════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩════════╝");
    else
        Console.WriteLine("╠══════╬══════════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬════════╣");
}
