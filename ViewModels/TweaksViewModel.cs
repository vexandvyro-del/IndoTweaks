using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoTweaks.Models;
using IndoTweaks.Services;

namespace IndoTweaks.ViewModels;

public partial class TweaksViewModel : ObservableObject
{
    private readonly SystemTweakService _tweaks = new();

    public ObservableCollection<TweakItem> Tweaks { get; } = new();

    [ObservableProperty]
    private bool _isAdmin = App.IsRunningAsAdministrator();

    public TweaksViewModel()
    {
        BuildTweakList();
        RefreshStates();
    }

    private void BuildTweakList()
    {
        Tweaks.Add(new TweakItem
        {
            Id = "gpu_power",
            Title = "GPU Power Preference: Maximum Performance",
            Description = "Forces Windows' per-app graphics preference to 'High performance' for Fortnite, preventing the GPU from downclocking during matches.",
            Category = TweakCategory.GpuPower,
            EstimatedFpsGain = 8,
        });

        Tweaks.Add(new TweakItem
        {
            Id = "game_mode_hags",
            Title = "Game Mode + Hardware-Accelerated GPU Scheduling",
            Description = "Enables Windows Game Mode (deprioritizes background work) and HAGS (lets the GPU manage its own scheduling, cutting latency).",
            Category = TweakCategory.GameModeHags,
            EstimatedFpsGain = 4,
            EstimatedLatencyReductionMs = 2,
        });

        Tweaks.Add(new TweakItem
        {
            Id = "cpu_priority",
            Title = "CPU Priority: Fortnite -> High",
            Description = "Raises FortniteClient-Win64-Shipping.exe to High priority so it isn't starved of CPU time slices by background processes. Only affects the current session - requires Fortnite to be running.",
            Category = TweakCategory.CpuPriority,
            EstimatedFpsGain = 3,
            RequiresRestorePoint = false, // live process tweak, no persistent system change
        });

        Tweaks.Add(new TweakItem
        {
            Id = "timer_resolution",
            Title = "System Timer Resolution: 0.5ms",
            Description = "Forces the OS scheduling tick down from the ~15.6ms Windows default, reducing input-to-frame latency at a small (~1-2%) CPU power cost.",
            Category = TweakCategory.TimerResolution,
            EstimatedLatencyReductionMs = 6,
        });

        Tweaks.Add(new TweakItem
        {
            Id = "network_tcp",
            Title = "Network: Disable Nagle's Algorithm + Throttling Index",
            Description = "Sets TCPNoDelay/TcpAckFrequency on all interfaces and disables NetworkThrottlingIndex, reducing latency jitter from packet batching.",
            Category = TweakCategory.NetworkTcp,
            EstimatedLatencyReductionMs = 8,
        });

        Tweaks.Add(new TweakItem
        {
            Id = "visual_effects",
            Title = "Visual Effects: Best Performance Preset",
            Description = "Disables Windows UI animations, transparency, and shadows - frees up compositor overhead, especially on lower-end iGPUs/CPUs.",
            Category = TweakCategory.VisualEffects,
            EstimatedFpsGain = 2,
        });
    }

    [RelayCommand]
    private void RefreshStates()
    {
        foreach (var tweak in Tweaks)
        {
            tweak.State = tweak.Id switch
            {
                "gpu_power" => _tweaks.DetectGpuPowerPreference(GetFortniteExePathGuess()),
                "game_mode_hags" => MapGameModeHags(_tweaks.DetectGameModeAndHags()),
                "cpu_priority" => DetectCpuPriorityState(),
                "timer_resolution" => TweakState.Unknown, // no persistent flag to read; state is session-only
                "network_tcp" => _tweaks.DetectNetworkTweaks(),
                "visual_effects" => TweakState.Unknown,
                _ => TweakState.Unknown,
            };
        }
    }

    private static TweakState MapGameModeHags((bool gameModeOn, bool hagsOn) state) =>
        state switch
        {
            (true, true) => TweakState.Applied,
            (false, false) => TweakState.NotApplied,
            _ => TweakState.PartiallyApplied,
        };

    private static TweakState DetectCpuPriorityState()
    {
        var proc = Process.GetProcessesByName("FortniteClient-Win64-Shipping").FirstOrDefault();
        if (proc is null) return TweakState.Unknown;
        return proc.PriorityClass == ProcessPriorityClass.High ? TweakState.Applied : TweakState.NotApplied;
    }

    private static string GetFortniteExePathGuess() =>
        @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe";

    [RelayCommand]
    private async Task ApplyTweakAsync(TweakItem tweak)
    {
        if (!IsAdmin)
        {
            MessageBox.Show("Restart IndoTweaks as Administrator to apply system tweaks.",
                "Admin Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (tweak.RequiresRestorePoint)
        {
            var confirm = MessageBox.Show(
                $"IndoTweaks will create a System Restore point before applying \"{tweak.Title}\".\n\n" +
                "This may take a few seconds. Continue?",
                "Create Restore Point?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            tweak.IsBusy = true;
            var result = await Task.Run(() => _tweaks.EnsureRestorePoint(tweak.Title));
            if (result == RestorePointResult.Failed)
            {
                tweak.IsBusy = false;
                var proceedAnyway = MessageBox.Show(
                    "Restore point creation failed. Apply the tweak anyway? (Not recommended.)",
                    "Restore Point Failed", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceedAnyway != MessageBoxResult.Yes) return;
            }
        }

        tweak.IsBusy = true;
        try
        {
            await Task.Run(() => Apply(tweak));
            tweak.LastActionLog = $"Applied at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            tweak.LastActionLog = $"Failed: {ex.Message}";
            LoggingService.Instance.Error($"Failed applying {tweak.Title}", ex);
        }
        finally
        {
            tweak.IsBusy = false;
            RefreshStates();
        }
    }

    [RelayCommand]
    private async Task RevertTweakAsync(TweakItem tweak)
    {
        tweak.IsBusy = true;
        try
        {
            await Task.Run(() => Revert(tweak));
            tweak.LastActionLog = $"Reverted at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            tweak.LastActionLog = $"Revert failed: {ex.Message}";
            LoggingService.Instance.Error($"Failed reverting {tweak.Title}", ex);
        }
        finally
        {
            tweak.IsBusy = false;
            RefreshStates();
        }
    }

    private void Apply(TweakItem tweak)
    {
        switch (tweak.Id)
        {
            case "gpu_power": _tweaks.ApplyGpuPowerPreference(GetFortniteExePathGuess()); break;
            case "game_mode_hags": _tweaks.ApplyGameModeAndHags(); break;
            case "cpu_priority":
                _tweaks.TrySetFortnitePriorityHigh(out var msg);
                LoggingService.Instance.Info(msg);
                break;
            case "timer_resolution": _tweaks.ApplyTimerResolution(useHalfMillisecond: true); break;
            case "network_tcp": _tweaks.ApplyNetworkTweaks(); break;
            case "visual_effects": _tweaks.ApplyPerformanceVisualEffects(); break;
        }
    }

    private void Revert(TweakItem tweak)
    {
        switch (tweak.Id)
        {
            case "gpu_power": _tweaks.RevertGpuPowerPreference(GetFortniteExePathGuess()); break;
            case "game_mode_hags": _tweaks.RevertGameModeAndHags(); break;
            case "timer_resolution": _tweaks.RevertTimerResolution(); break;
            case "network_tcp": _tweaks.RevertNetworkTweaks(); break;
            case "visual_effects": _tweaks.RevertPerformanceVisualEffects(); break;
        }
    }
}
