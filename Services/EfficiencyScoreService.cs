using IndoTweaks.Models;

namespace IndoTweaks.Services;

public sealed record EfficiencyReport(
    int ScorePercent,
    int TotalFpsPenalty,
    int TotalLatencyPenaltyMs,
    IReadOnlyList<ImpactFinding> Findings
);

/// <summary>
/// The "Missing Gains" engine: takes the current TweakItem states + Fortnite config
/// mismatches and turns them into a single 0-100 System Efficiency Score plus a
/// ranked list of specific findings with their estimated FPS/latency cost.
///
/// The FPS/latency numbers per tweak are estimates based on commonly cited community
/// benchmarks (not guaranteed per-system) - they're meant to rank-order priority,
/// not promise an exact number. Surface that caveat in the UI.
/// </summary>
public sealed class EfficiencyScoreService
{
    // Weight = how many "points" out of 100 this tweak is worth when NOT applied.
    // Must sum to <= 100 across all tweaks; remaining budget = headroom for
    // things this app can't detect (e.g. actual driver version, monitor refresh rate).
    private static readonly Dictionary<TweakCategory, int> CategoryWeight = new()
    {
        [TweakCategory.GpuPower] = 15,
        [TweakCategory.GameModeHags] = 10,
        [TweakCategory.CpuPriority] = 10,
        [TweakCategory.TimerResolution] = 15,
        [TweakCategory.NetworkTcp] = 20,
        [TweakCategory.VisualEffects] = 15,
        [TweakCategory.Startup] = 5,
    };

    public EfficiencyReport BuildReport(
        IEnumerable<TweakItem> tweaks,
        IEnumerable<(FortniteSettingDefinition Def, string CurrentValue)> fortniteMismatches)
    {
        var findings = new List<ImpactFinding>();
        int score = 100;

        foreach (var tweak in tweaks)
        {
            if (tweak.State == TweakState.Applied) continue;

            int weight = CategoryWeight.GetValueOrDefault(tweak.Category, 5);
            int deduction = tweak.State == TweakState.PartiallyApplied ? weight / 2 : weight;
            score -= deduction;

            findings.Add(new ImpactFinding(
                SettingName: tweak.Title,
                CurrentValue: tweak.State.ToString(),
                RecommendedValue: "Applied",
                FpsPenalty: tweak.EstimatedFpsGain,
                LatencyPenaltyMs: tweak.EstimatedLatencyReductionMs,
                Category: tweak.Category.ToString()
            ));
        }

        foreach (var (def, currentValue) in fortniteMismatches)
        {
            // Fortnite config mismatches share a fixed small deduction each so one
            // missed graphics setting doesn't tank the whole score.
            score -= 2;
            findings.Add(new ImpactFinding(
                SettingName: def.DisplayName,
                CurrentValue: currentValue,
                RecommendedValue: def.RecommendedValue,
                FpsPenalty: EstimateFpsForFortniteSetting(def.Key),
                LatencyPenaltyMs: 0,
                Category: "Fortnite Settings"
            ));
        }

        score = Math.Clamp(score, 0, 100);

        return new EfficiencyReport(
            ScorePercent: score,
            TotalFpsPenalty: findings.Sum(f => f.FpsPenalty),
            TotalLatencyPenaltyMs: findings.Sum(f => f.LatencyPenaltyMs),
            Findings: findings.OrderByDescending(f => f.FpsPenalty + f.LatencyPenaltyMs * 2).ToList()
        );
    }

    // Rough, order-of-magnitude community-benchmark estimates per setting - used only
    // for ranking findings, always labeled as estimates in the UI.
    private static int EstimateFpsForFortniteSetting(string settingKey) => settingKey switch
    {
        "ShadowQuality" => 12,
        "PostProcessingQuality" => 6,
        "EffectsQuality" => 5,
        "ViewDistanceQuality" => 0, // set to Epic intentionally; not a "penalty" to fix
        "TextureQuality" => 2,
        "bUseVSync" => 0,
        "MotionBlur" => 0,
        _ => 1,
    };
}
