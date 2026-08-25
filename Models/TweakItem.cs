using CommunityToolkit.Mvvm.ComponentModel;

namespace IndoTweaks.Models;

public enum TweakCategory
{
    GpuPower,
    GameModeHags,
    CpuPriority,
    TimerResolution,
    NetworkTcp,
    VisualEffects,
    Startup
}

public enum TweakState
{
    Unknown,
    NotApplied,   // Red - needs fix
    PartiallyApplied, // Yellow
    Applied       // Green - optimal
}

/// <summary>
/// A single safe/reversible system tweak shown on the Tweaks & Optimizations tab.
/// Detect() figures out current state; Apply()/Revert() do the actual work and
/// are implemented per-tweak in SystemTweakService.
/// </summary>
public partial class TweakItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TweakCategory Category { get; init; }

    /// <summary>Estimated FPS gain if this tweak is applied (0 if latency-only).</summary>
    public int EstimatedFpsGain { get; init; }

    /// <summary>Estimated latency reduction in ms if applied (0 if FPS-only).</summary>
    public int EstimatedLatencyReductionMs { get; init; }

    [ObservableProperty]
    private TweakState _state = TweakState.Unknown;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _lastActionLog = string.Empty;

    /// <summary>Whether reverting requires a restore point warning (registry/system-level changes do).</summary>
    public bool RequiresRestorePoint { get; init; } = true;
}
