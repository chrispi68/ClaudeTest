using ClaudeTest;

DateTime startzeit = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);

// EnergyDataList erstellen mit 72 Stunden (Index 0 = aktuelle Stunde)
var EnergyDataList = new List<EnergyData>();
for (int i = 0; i < 72; i++)
{
    EnergyDataList.Add(new EnergyData { Zeitstempel = startzeit.AddHours(i) });
}

Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          Energieprognose – nächste 72 Stunden                                    ║");
Console.WriteLine("╠══════╦══════════╦═════════════════╦══════════════╦══════════╦══════════╦═════════╣");
Console.WriteLine("║  Std ║ Uhrzeit  ║ PV-Ertrag (kW)  ║ Verbrauch(kW)║ WP  (kW) ║ Batt.(kW)║ Einsp.  ║");
Console.WriteLine("╠══════╬══════════╬═════════════════╬══════════════╬══════════╬══════════╬═════════╣");

for (int i = 0; i < EnergyDataList.Count; i++)
{
    var d = EnergyDataList[i];
    string marker = i == 0 ? "►" : " ";
    Console.WriteLine(
        $"║ {marker}{i,3} ║ {d.Zeitstempel:dd.MM. HH:mm} ║ {d.PVErtrag,10:F2} kW   ║ {d.Hausverbrauch,8:F2} kW ║ {d.Wärmepumpe,5:F2} kW║ {d.Batterie,5:F2} kW║ {d.Einspeisung,4:F2} kW║"
    );
}

Console.WriteLine("╚══════╩══════════╩═════════════════╩══════════════╩══════════╩══════════╩═════════╝");
Console.WriteLine($"\nNetzbezug aller Stunden: 0,00 kW (alle Werte sind noch 0)");
