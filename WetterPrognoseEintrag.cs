using System.Text.Json.Serialization;

namespace ClaudeTest;

public class WetterPrognoseEintrag
{
    [JsonPropertyName("datetime")]
    public DateTimeOffset Zeitpunkt { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperatur { get; set; }
}
