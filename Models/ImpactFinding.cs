namespace IndoTweaks.Models;

/// <summary>
/// One line item in the "Missing Gains" report: a specific suboptimal
/// setting paired with its estimated performance cost.
/// </summary>
public sealed record ImpactFinding(
    string SettingName,
    string CurrentValue,
    string RecommendedValue,
    int FpsPenalty,
    int LatencyPenaltyMs,
    string Category
)
{
    public string PenaltyLabel
    {
        get
        {
            var parts = new List<string>();
            if (FpsPenalty > 0) parts.Add($"-{FpsPenalty} FPS");
            if (LatencyPenaltyMs > 0) parts.Add($"+{LatencyPenaltyMs}ms Latency");
            return parts.Count == 0 ? "No measurable penalty" : string.Join("  ·  ", parts);
        }
    }
}
