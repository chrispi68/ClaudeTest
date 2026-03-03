using System.Text.Json;

namespace ClaudeTest;

public class HomeAssistantService
{
    private readonly HomeAssistantConfig _config;
    private readonly HttpClient _httpClient;

    public HomeAssistantService(HomeAssistantConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiToken}");
    }

    public async Task<double> GetBatterieladungKwhAsync()
    {
        string url = $"{_config.Url.TrimEnd('/')}/api/states/{_config.BatterieSensor}";
        string json = await _httpClient.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);
        string? state = doc.RootElement.GetProperty("state").GetString();

        if (!double.TryParse(state, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double wert))
            throw new InvalidDataException($"Ungültiger Sensorwert: '{state}'");

        return wert;
    }
}
