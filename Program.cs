using System.Text.Json;
using ClaudeTest;

// Konfiguration laden
var configJson = File.ReadAllText("appsettings.json");
var config = JsonSerializer.Deserialize<AppConfig>(configJson,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("appsettings.json konnte nicht geladen werden.");

// Batterieladung aus Home Assistant abrufen
double initialBatterieladung = 0.0;
try
{
    var haService = new HomeAssistantService(config.HomeAssistant);
    initialBatterieladung = await haService.GetBatterieladungKwhAsync();
    Console.WriteLine($"Batterieladung aus Home Assistant: {initialBatterieladung:F2} kWh");
}
catch (Exception ex)
{
    Console.WriteLine($"Warnung: HA nicht erreichbar ({ex.Message}). Starte mit 0 kWh.");
}

// PV-Testdaten – gleiche Tageswerte für alle 3 Tage (05./06./07.03.2026)
var tagesWerte = new[]
{
    (h:  1, est: 0.0),
    (h:  2, est: 0.0),
    (h:  3, est: 0.0),
    (h:  4, est: 0.0),
    (h:  5, est: 0.0),
    (h:  6, est: 0.0),
    (h:  7, est: 0.5625),
    (h:  8, est: 2.5938),
    (h:  9, est: 5.3486),
    (h: 10, est: 8.2889),
    (h: 11, est: 10.4266),
    (h: 12, est: 11.6748),
    (h: 13, est: 11.8644),
    (h: 14, est: 11.1378),
    (h: 15, est: 9.4034),
    (h: 16, est: 6.6757),
    (h: 17, est: 3.0162),
    (h: 18, est: 0.0483),
    (h: 19, est: 0.0),
    (h: 20, est: 0.0),
    (h: 21, est: 0.0),
    (h: 22, est: 0.0),
    (h: 23, est: 0.0),
};

var pvPrognose = new List<PVPrognoseEintrag>();
var startDatum = DateOnly.FromDateTime(DateTime.Now);
for (int tag = 0; tag < 4; tag++)
{
    var datum = startDatum.AddDays(tag);
    foreach (var w in tagesWerte)
    {
        pvPrognose.Add(new PVPrognoseEintrag
        {
            PeriodStart = new DateTimeOffset(datum.Year, datum.Month, datum.Day, w.h, 0, 0, TimeSpan.FromHours(1)),
            PVEstimate  = w.est
        });
    }
}

var wärmepumpenverbrauch = new Dictionary<int, double>
{
    { 0, 1.5 }, { 1, 1.5 }, { 2, 1.5 }, { 3, 1.5 },
    { 6, 2.0 }, { 7, 2.0 }, { 18, 2.0 }, { 19, 2.0 }, { 20, 2.0 }
};

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
    initialBatterieladungKw: initialBatterieladung
);

var EnergyDataList = service.EnergyDataList;

Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Energieprognose – nächste 72 Stunden   |  Max. Batteriekapazität: {service.MaxBatteriekapazität,4:F1} kW                               ║");
Console.WriteLine("╠══════╦══════════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╣");
Console.WriteLine("║  Std ║ Datum/Zeit   ║ PV  (kW) ║Haus (kW) ║ WP  (kW) ║Basis(kW) ║Verb.(kW) ║Batt.(kWh)║Einsp(kW) ║Netz (kW) ║");
Console.WriteLine("╠══════╬══════════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╣");

for (int i = 0; i < EnergyDataList.Count; i++)
{
    var d = EnergyDataList[i];
    string marker = i == 0 ? "►" : " ";
    double gesamtverbrauch = d.Basisverbrauch + d.Hausverbrauch + d.Wärmepumpe;
    Console.WriteLine(
        $"║ {marker}{i,3} ║ {d.Zeitstempel:dd.MM. HH:mm}  ║ {d.PVErtrag,5:F2} kW ║ {d.Hausverbrauch,5:F2} kW ║ {d.Wärmepumpe,5:F2} kW ║ {d.Basisverbrauch,5:F2} kW ║ {gesamtverbrauch,5:F2} kW ║ {d.Batterie,5:F2} kWh║ {d.Einspeisung,5:F2} kW ║ {d.Netzbezug,5:F2} kW ║"
    );
}

Console.WriteLine("╚══════╩══════════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╝");
