namespace ClaudeTest;

public class AppConfig
{
    public HomeAssistantConfig HomeAssistant { get; set; } = new();
}

public class HomeAssistantConfig
{
    public string Url { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string BatterieSensor { get; set; } = string.Empty;
}
