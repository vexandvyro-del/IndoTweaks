namespace IndoTweaks.Models;

/// <summary>
/// Immutable snapshot of hardware sensor readings at one poll tick.
/// Produced by HardwareMonitorService, consumed by DashboardViewModel
/// and EfficiencyScoreService.
/// </summary>
public sealed record HardwareSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    // CPU
    public string CpuName { get; init; } = "Unknown CPU";
    public float CpuTemperatureC { get; init; }
    public float CpuLoadPercent { get; init; }
    public float CpuClockMHz { get; init; }
    public float CpuPackagePowerW { get; init; }

    // GPU
    public string GpuName { get; init; } = "Unknown GPU";
    public float GpuTemperatureC { get; init; }
    public float GpuLoadPercent { get; init; }
    public float GpuClockMHz { get; init; }
    public float GpuFanRpm { get; init; }
    public float GpuFanPercent { get; init; }
    public float GpuVramUsedMB { get; init; }
    public float GpuVramTotalMB { get; init; }

    // Memory
    public float RamTotalMB { get; init; }
    public float RamAvailableMB { get; init; }
    public float RamUsedPercent => RamTotalMB <= 0 ? 0 : 100f * (RamTotalMB - RamAvailableMB) / RamTotalMB;

    // Network
    public double PingMs { get; init; }
    public double JitterMs { get; init; }
    public double PacketLossPercent { get; init; }
}
