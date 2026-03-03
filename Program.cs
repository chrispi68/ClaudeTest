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
double standortLat = 50.5191;  // Fallback: Schleiden, Nordrhein-Westfalen
double standortLon = 6.3977;
bool istJemandZuhause = true;
HomeAssistantService? haService = null;

try
{
    haService = new HomeAssistantService(config.HomeAssistant);

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

    var standort = await haService.GetStandortAsync();
    standortLat = standort.Latitude;
    standortLon = standort.Longitude;
    Console.WriteLine($"Standort:                           {standortLat:F4}°N, {standortLon:F4}°E");

    istJemandZuhause = await haService.GetIstJemandZuhauseAsync(config.HomeAssistant.AnwesenheitsSensor);
    Console.WriteLine($"Anwesenheit:                        {(istJemandZuhause ? "Jemand zu Hause" : "Niemand zu Hause")}");
}
catch (Exception ex)
{
    Console.WriteLine($"Warnung: HA nicht erreichbar ({ex.Message}). Starte mit Standardwerten.");
}

var hausverbrauch = new HausverbrauchService(standortLat, standortLon, istJemandZuhause).BerechnePrognose();

// Service initialisieren
var service = new EnergyService(
    basisverbrauchKw: 0.6,
    maxBatteriekapazitätKw: config.MaxBatteriekapazitätKwh,
    pvPrognose: pvPrognose,
    wärmepumpenverbrauch: wärmepumpenverbrauch,
    hausverbrauch: hausverbrauch,
    initialBatterieladungKw: initialBatterieladung,
    aussentemperaturen: aussentemperaturen
);

var EnergyDataList = service.EnergyDataList;

Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Energieprognose – nächste 72 Stunden   |  Max. Batteriekapazität: {config.MaxBatteriekapazitätKwh,4:F1} kWh                                  ║");
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
        Console.Write($"║ {marker}{i,3} ║ {d.Zeitstempel:dd.MM. HH:mm}  ║ {d.PVErtrag,5:F2} kW ║ {d.Hausverbrauch,5:F2} kW ║ {d.Wärmepumpe,5:F2} kW ║ {d.Basisverbrauch,5:F2} kW ║ {gesamtverbrauch,5:F2} kW ║ ");
        Console.ForegroundColor = d.Batterie > 0 ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write($"{d.Batterie,5:F2} kWh");
        Console.ResetColor();
        Console.WriteLine($"║ {d.Einspeisung,5:F2} kW ║ {d.Netzbezug,5:F2} kW ║{d.Aussentemperatur,5:F1}°C ║");
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

// Prognose an Home Assistant zurückspielen
if (haService != null)
{
    try
    {
        await haService.SendEnergiePrognoseAsync(EnergyDataList);
        Console.WriteLine("Prognose erfolgreich an Home Assistant übermittelt (sensor.strom_energieprognose).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warnung: Prognose konnte nicht übermittelt werden ({ex.Message}).");
    }
}
