using System.Windows.Threading;
using IndoTweaks.Models;
using LibreHardwareMonitor.Hardware;

namespace IndoTweaks.Services;

/// <summary>
/// Wraps LibreHardwareMonitorLib's Computer object, polls it on a background-friendly
/// timer, and projects raw sensor trees into a flat, UI-friendly HardwareSnapshot.
///
/// LibreHardwareMonitor requires Administrator privileges to read most CPU/GPU sensors
/// (ring0 driver access) - if not elevated, temperature/power readings will come back
/// as null and this service degrades gracefully (0 values, logged once).
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly DispatcherTimer _timer;
    private bool _warnedAboutElevation;

    public event Action<HardwareSnapshot>? SnapshotReady;

    public HardwareMonitorService(TimeSpan? pollInterval = null)
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true, // fan controllers
        };
        _computer.Open();

        _timer = new DispatcherTimer
        {
            Interval = pollInterval ?? TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Poll()
    {
        try
        {
            var snapshot = BuildSnapshot();
            SnapshotReady?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("Hardware poll failed", ex);
        }
    }

    private HardwareSnapshot BuildSnapshot()
    {
        string cpuName = "Unknown CPU";
        float cpuTemp = 0, cpuLoad = 0, cpuClock = 0, cpuPower = 0;

        string gpuName = "Unknown GPU";
        float gpuTemp = 0, gpuLoad = 0, gpuClock = 0, gpuFanRpm = 0, gpuFanPct = 0;
        float vramUsed = 0, vramTotal = 0;

        float ramTotal = 0, ramAvailable = 0;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    cpuName = hardware.Name;
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Value is null) continue;
                        switch (sensor.SensorType)
                        {
                            case SensorType.Temperature when sensor.Name.Contains("Package") || sensor.Name.Contains("Average"):
                                cpuTemp = sensor.Value.Value;
                                break;
                            case SensorType.Load when sensor.Name.Contains("Total"):
                                cpuLoad = sensor.Value.Value;
                                break;
                            case SensorType.Clock when cpuClock == 0: // first core clock as representative
                                cpuClock = sensor.Value.Value;
                                break;
                            case SensorType.Power when sensor.Name.Contains("Package"):
                                cpuPower = sensor.Value.Value;
                                break;
                        }
                    }
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    gpuName = hardware.Name;
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Value is null) continue;
                        switch (sensor.SensorType)
                        {
                            case SensorType.Temperature when sensor.Name.Contains("Core") || sensor.Name.Contains("Hot Spot") is false && gpuTemp == 0:
                                gpuTemp = sensor.Value.Value;
                                break;
                            case SensorType.Load when sensor.Name.Contains("Core"):
                                gpuLoad = sensor.Value.Value;
                                break;
                            case SensorType.Clock when sensor.Name.Contains("Core"):
                                gpuClock = sensor.Value.Value;
                                break;
                            case SensorType.Fan:
                                gpuFanRpm = sensor.Value.Value;
                                break;
                            case SensorType.Control when sensor.Name.Contains("Fan"):
                                gpuFanPct = sensor.Value.Value;
                                break;
                            case SensorType.SmallData when sensor.Name.Contains("Memory Used"):
                                vramUsed = sensor.Value.Value;
                                break;
                            case SensorType.SmallData when sensor.Name.Contains("Memory Total"):
                                vramTotal = sensor.Value.Value;
                                break;
                        }
                    }
                    break;

                case HardwareType.Memory:
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.Value is null) continue;
                        if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                            ramTotal = ramTotal; // placeholder; total computed below from Available+Used
                        if (sensor.Name.Contains("Memory Available") && sensor.SensorType == SensorType.Data)
                            ramAvailable = sensor.Value.Value * 1024f; // GB -> MB
                        if (sensor.Name.Contains("Memory Used") && sensor.SensorType == SensorType.Data)
                            ramTotal += sensor.Value.Value * 1024f;
                    }
                    break;
            }
        }

        // Total RAM is most reliably pulled from GC/OS info rather than sensor subtraction.
        var memStatus = new NativeMethods.MEMORYSTATUSEX();
        if (NativeMethods.GlobalMemoryStatusEx(memStatus))
        {
            ramTotal = memStatus.ullTotalPhys / (1024f * 1024f);
            ramAvailable = memStatus.ullAvailPhys / (1024f * 1024f);
        }

        if (!_warnedAboutElevation && cpuTemp == 0 && gpuTemp == 0)
        {
            _warnedAboutElevation = true;
            LoggingService.Instance.Warn(
                "Temperature sensors returned no data - IndoTweaks likely isn't running elevated. " +
                "Restart as Administrator for full telemetry.");
        }

        return new HardwareSnapshot
        {
            CpuName = cpuName,
            CpuTemperatureC = cpuTemp,
            CpuLoadPercent = cpuLoad,
            CpuClockMHz = cpuClock,
            CpuPackagePowerW = cpuPower,

            GpuName = gpuName,
            GpuTemperatureC = gpuTemp,
            GpuLoadPercent = gpuLoad,
            GpuClockMHz = gpuClock,
            GpuFanRpm = gpuFanRpm,
            GpuFanPercent = gpuFanPct,
            GpuVramUsedMB = vramUsed,
            GpuVramTotalMB = vramTotal,

            RamTotalMB = ramTotal,
            RamAvailableMB = ramAvailable,
        };
    }

    public void Dispose()
    {
        _timer.Stop();
        _computer.Close();
    }
}
