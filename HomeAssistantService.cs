using System.Text.Json;

namespace ClaudeTest;

public class HomeAssistantService
{
    private readonly HomeAssistantConfig _config;
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HomeAssistantService(HomeAssistantConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiToken}");
    }

    public async Task<double> GetBatterieladungKwhAsync()
    {
        string state = await GetStateAsync(_config.BatterieSensor);

        if (!double.TryParse(state, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double wert))
            throw new InvalidDataException($"Ungültiger Sensorwert: '{state}'");

        return wert;
    }

    public async Task<WärmepumpeKonfig> GetWärmepumpeKonfigAsync(WärmepumpeSensorenConfig sensoren)
    {
        string betriebsart = await GetStateAsync(sensoren.Betriebsart);

        string sollwertSensor = betriebsart == "Komfort"
            ? sensoren.KomfortSollwert
            : sensoren.ReduziertSollwert;

        double aussentemperatur = double.Parse(await GetStateAsync(sensoren.Aussentemperatur),
            System.Globalization.CultureInfo.InvariantCulture);
        double solltemperatur = double.Parse(await GetStateAsync(sollwertSensor),
            System.Globalization.CultureInfo.InvariantCulture);

        return new WärmepumpeKonfig
        {
            Aussentemperatur   = aussentemperatur,
            SolltemperaturInnen = solltemperatur,
            Betriebsart        = betriebsart
        };
    }

    public async Task<bool> GetIstJemandZuhauseAsync(string sensor)
    {
        string state = await GetStateAsync(sensor);
        return state == "on";
    }

    public async Task<(double Latitude, double Longitude)> GetStandortAsync()
    {
        string url = $"{_config.Url.TrimEnd('/')}/api/config";
        string json = await _httpClient.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        double lat = doc.RootElement.GetProperty("latitude").GetDouble();
        double lon = doc.RootElement.GetProperty("longitude").GetDouble();
        return (lat, lon);
    }

    private async Task<string> GetStateAsync(string entityId)
    {
        string url = $"{_config.Url.TrimEnd('/')}/api/states/{entityId}";
        string json = await _httpClient.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("state").GetString()
            ?? throw new InvalidDataException($"Kein State für '{entityId}'");
    }

    public async Task<List<WetterPrognoseEintrag>> GetWetterPrognoseAsync(string wetterSensor)
    {
        string url = $"{_config.Url.TrimEnd('/')}/api/services/weather/get_forecasts?return_response";
        var body = new StringContent(
            JsonSerializer.Serialize(new { entity_id = wetterSensor, type = "hourly" }),
            System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, body);
        string json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("service_response", out var serviceResponse))
            return new List<WetterPrognoseEintrag>();
        if (!serviceResponse.TryGetProperty(wetterSensor, out var sensorData))
            return new List<WetterPrognoseEintrag>();
        if (!sensorData.TryGetProperty("forecast", out var forecast))
            return new List<WetterPrognoseEintrag>();

        return JsonSerializer.Deserialize<List<WetterPrognoseEintrag>>(forecast.GetRawText(), _jsonOptions)
               ?? new List<WetterPrognoseEintrag>();
    }

    public async Task<List<PVPrognoseEintrag>> GetPVPrognoseAsync()
    {
        var result = new Dictionary<DateTimeOffset, PVPrognoseEintrag>();

        foreach (string sensor in _config.PVSensoren)
        {
            string url = $"{_config.Url.TrimEnd('/')}/api/states/{sensor}";
            string json = await _httpClient.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.GetProperty("attributes").TryGetProperty("detailedForecast", out var forecast))
                continue;

            foreach (var entry in forecast.EnumerateArray())
            {
                var eintrag = JsonSerializer.Deserialize<PVPrognoseEintrag>(entry.GetRawText(), _jsonOptions);
                if (eintrag != null)
                    result[eintrag.PeriodStart] = eintrag;
            }
        }

        return result.Values.OrderBy(e => e.PeriodStart).ToList();
    }
}
