using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoTweaks.Models;
using IndoTweaks.Services;

namespace IndoTweaks.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly HardwareMonitorService _hardwareMonitor;
    private readonly NetworkService _network;
    private readonly System.Windows.Threading.DispatcherTimer _networkTimer;

    [ObservableProperty] private float _cpuTemp;
    [ObservableProperty] private float _cpuLoad;
    [ObservableProperty] private float _cpuClock;
    [ObservableProperty] private string _cpuName = "Detecting...";

    [ObservableProperty] private float _gpuTemp;
    [ObservableProperty] private float _gpuLoad;
    [ObservableProperty] private float _gpuClock;
    [ObservableProperty] private float _gpuFanRpm;
    [ObservableProperty] private string _gpuName = "Detecting...";

    [ObservableProperty] private float _ramUsedPercent;
    [ObservableProperty] private float _ramAvailableMB;
    [ObservableProperty] private float _ramTotalMB;

    [ObservableProperty] private double _pingMs;
    [ObservableProperty] private double _jitterMs;
    [ObservableProperty] private double _packetLossPercent;

    [ObservableProperty] private bool _isFlushingMemory;

    public DashboardViewModel()
    {
        _hardwareMonitor = new HardwareMonitorService();
        _hardwareMonitor.SnapshotReady += OnSnapshot;
        _hardwareMonitor.Start();

        _network = new NetworkService();
        _networkTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _networkTimer.Tick += async (_, _) => await SampleNetworkAsync();
        _networkTimer.Start();
    }

    private void OnSnapshot(HardwareSnapshot snap)
    {
        CpuName = snap.CpuName;
        CpuTemp = snap.CpuTemperatureC;
        CpuLoad = snap.CpuLoadPercent;
        CpuClock = snap.CpuClockMHz;

        GpuName = snap.GpuName;
        GpuTemp = snap.GpuTemperatureC;
        GpuLoad = snap.GpuLoadPercent;
        GpuClock = snap.GpuClockMHz;
        GpuFanRpm = snap.GpuFanRpm;

        RamUsedPercent = snap.RamUsedPercent;
        RamAvailableMB = snap.RamAvailableMB;
        RamTotalMB = snap.RamTotalMB;
    }

    private async Task SampleNetworkAsync()
    {
        var (avg, jitter, loss) = await _network.SampleAsync();
        PingMs = avg;
        JitterMs = jitter;
        PacketLossPercent = loss;
    }

    [RelayCommand]
    private async Task FlushStandbyMemoryAsync()
    {
        IsFlushingMemory = true;
        try
        {
            await MemoryFlushService.FlushStandbyListAsync();
            LoggingService.Instance.Action("Flushed standby memory list.");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("Failed to flush standby memory", ex);
        }
        finally
        {
            IsFlushingMemory = false;
        }
    }

    public void Dispose()
    {
        _hardwareMonitor.SnapshotReady -= OnSnapshot;
        _hardwareMonitor.Dispose();
        _networkTimer.Stop();
    }
}
