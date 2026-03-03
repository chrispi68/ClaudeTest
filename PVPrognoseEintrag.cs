using System.Text.Json.Serialization;

namespace ClaudeTest;

public class PVPrognoseEintrag
{
    [JsonPropertyName("period_start")]
    public DateTimeOffset PeriodStart { get; set; }

    [JsonPropertyName("pv_estimate")]
    public double PVEstimate { get; set; }
}
