namespace ClaudeTest;

public class AppConfig
{
    public HomeAssistantConfig HomeAssistant { get; set; } = new();
    public double MaxBatteriekapazitätKwh { get; set; } = 15.0;
}

public class HomeAssistantConfig
{
    public string Url { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string BatterieSensor { get; set; } = string.Empty;
    public List<string> PVSensoren { get; set; } = new();
    public WärmepumpeSensorenConfig Wärmepumpe { get; set; } = new();
    public string WetterSensor { get; set; } = string.Empty;
    public string AnwesenheitsSensor { get; set; } = string.Empty;
}

public class WärmepumpeSensorenConfig
{
    public string Aussentemperatur { get; set; } = string.Empty;
    public string KomfortSollwert { get; set; } = string.Empty;
    public string ReduziertSollwert { get; set; } = string.Empty;
    public string Betriebsart { get; set; } = string.Empty;
}
