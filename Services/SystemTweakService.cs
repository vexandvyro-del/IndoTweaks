using Microsoft.Win32;
using System.Diagnostics;
using IndoTweaks.Models;

namespace IndoTweaks.Services;

/// <summary>
/// Implements every tweak listed on the Tweaks & Optimizations tab.
///
/// Design contract for every tweak method:
///   Detect{Name}()  -> reads current registry/system state, returns TweakState
///   Apply{Name}()   -> writes the optimal value; backs up the previous value first
///   Revert{Name}()  -> restores the previously backed-up value
///
/// All registry writes go through SetValueBackedUp() which stores the prior value
/// in a dedicated IndoTweaks backup key so Revert always has something to restore to,
/// even across app restarts.
/// </summary>
public sealed class SystemTweakService
{
    private const string BackupRootKey = @"SOFTWARE\IndoTweaks\Backups";
    private readonly RestorePointService _restorePoints = new();

    // ============================================================
    // 1. GPU Power Management -> "Prefer Maximum Performance"
    // ============================================================
    // NVIDIA/AMD control panels store this per-app or globally; the safe, driver-agnostic
    // equivalent Windows itself exposes is the "Graphics performance preference" added in
    // Win10 2004+, keyed by exe path under this registry value.
    private const string GraphicsPrefKey = @"SOFTWARE\Microsoft\DirectX\UserGpuPreferences";

    public TweakState DetectGpuPowerPreference(string fortniteExePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(GraphicsPrefKey);
        var value = key?.GetValue(fortniteExePath) as string;
        // Windows stores e.g. "GpuPreference=2;" where 2 = High performance
        return value?.Contains("GpuPreference=2") == true ? TweakState.Applied : TweakState.NotApplied;
    }

    public void ApplyGpuPowerPreference(string fortniteExePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(GraphicsPrefKey);
        BackupValue(Registry.CurrentUser, GraphicsPrefKey, fortniteExePath);
        key.SetValue(fortniteExePath, "GpuPreference=2;", RegistryValueKind.String);
        LoggingService.Instance.Action($"Set GPU preference to High Performance for {fortniteExePath}");
    }

    public void RevertGpuPowerPreference(string fortniteExePath) =>
        RestoreValue(Registry.CurrentUser, GraphicsPrefKey, fortniteExePath);

    // ============================================================
    // 2. Game Mode & HAGS status (read-only checker + one-click fix)
    // ============================================================
    private const string GameModeKey = @"SOFTWARE\Microsoft\GameBar";
    private const string HagsKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    public (bool gameModeOn, bool hagsOn) DetectGameModeAndHags()
    {
        bool gameMode = (Registry.CurrentUser.OpenSubKey(GameModeKey)?.GetValue("AutoGameModeEnabled") as int?) == 1;
        bool hags = (Registry.LocalMachine.OpenSubKey(HagsKey)?.GetValue("HwSchMode") as int?) == 2;
        return (gameMode, hags);
    }

    public void ApplyGameModeAndHags()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(GameModeKey))
        {
            BackupValue(Registry.CurrentUser, GameModeKey, "AutoGameModeEnabled");
            key.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
        }
        using (var key = Registry.LocalMachine.CreateSubKey(HagsKey))
        {
            BackupValue(Registry.LocalMachine, HagsKey, "HwSchMode");
            key.SetValue("HwSchMode", 2, RegistryValueKind.DWord);
        }
        LoggingService.Instance.Action("Enabled Game Mode and Hardware-Accelerated GPU Scheduling (reboot required for HAGS to take effect).");
    }

    public void RevertGameModeAndHags()
    {
        RestoreValue(Registry.CurrentUser, GameModeKey, "AutoGameModeEnabled");
        RestoreValue(Registry.LocalMachine, HagsKey, "HwSchMode");
    }

    // ============================================================
    // 3. CPU Priority for Fortnite process
    // ============================================================
    // Applied live to the running process (ProcessPriorityClass) rather than a
    // permanent registry hook, so there is nothing "stuck" if Fortnite isn't running -
    // this only affects the current session's process.
    public bool TrySetFortnitePriorityHigh(out string message)
    {
        var proc = Process.GetProcessesByName("FortniteClient-Win64-Shipping").FirstOrDefault();
        if (proc is null)
        {
            message = "Fortnite isn't currently running - launch it first, then re-check this tweak.";
            return false;
        }

        try
        {
            proc.PriorityClass = ProcessPriorityClass.High; // one step below RealTime; avoids starving input/network threads
            message = $"Set FortniteClient-Win64-Shipping.exe (PID {proc.Id}) priority to High.";
            LoggingService.Instance.Action(message);
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to set process priority: {ex.Message}";
            LoggingService.Instance.Error(message, ex);
            return false;
        }
    }

    // ============================================================
    // 4. Timer Resolution
    // ============================================================
    private uint _previousTimerResolution;
    private bool _timerResolutionActive;

    public void ApplyTimerResolution(bool useHalfMillisecond)
    {
        uint requested = useHalfMillisecond ? 5000u : 10000u; // 100ns units: 0.5ms / 1ms
        uint current = 0;
        int status = NativeMethods.NtSetTimerResolution(requested, true, ref current);

        if (status == 0)
        {
            _previousTimerResolution = current;
            _timerResolutionActive = true;
            LoggingService.Instance.Action(
                $"System timer resolution set to {(useHalfMillisecond ? "0.5ms" : "1ms")} (was {current / 10000.0:0.0}ms).");
        }
        else
        {
            LoggingService.Instance.Error($"NtSetTimerResolution failed with status 0x{status:X8}");
        }
    }

    public void RevertTimerResolution()
    {
        if (!_timerResolutionActive) return;
        uint current = 0;
        NativeMethods.NtSetTimerResolution(_previousTimerResolution, false, ref current);
        _timerResolutionActive = false;
        LoggingService.Instance.Action("System timer resolution reverted to default.");
    }

    // ============================================================
    // 5. Network / TCP tweaks
    // ============================================================
    private const string TcpParamsKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
    private const string MMCSSKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    public TweakState DetectNetworkTweaks()
    {
        var netThrottle = Registry.LocalMachine.OpenSubKey(MMCSSKey)?.GetValue("NetworkThrottlingIndex") as int?;
        bool throttleDisabled = netThrottle == -1 || netThrottle == 0xFFFFFFFF;
        return throttleDisabled ? TweakState.Applied : TweakState.NotApplied;
    }

    public void ApplyNetworkTweaks()
    {
        // Disable the multimedia network throttling index (default caps non-multimedia
        // network traffic priority at ~10 packets/ms while a multimedia app is active).
        using (var key = Registry.LocalMachine.CreateSubKey(MMCSSKey))
        {
            BackupValue(Registry.LocalMachine, MMCSSKey, "NetworkThrottlingIndex");
            key.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
        }

        // Disable Nagle's Algorithm (TCPNoDelay) per network interface, plus
        // TcpAckFrequency=1 so ACKs aren't batched - both reduce jitter for
        // small, frequent packets like Fortnite's netcode.
        using var interfacesKey = Registry.LocalMachine.OpenSubKey(
            TcpParamsKey + @"\Interfaces", writable: true);

        if (interfacesKey != null)
        {
            foreach (var ifaceName in interfacesKey.GetSubKeyNames())
            {
                var ifacePath = TcpParamsKey + $@"\Interfaces\{ifaceName}";
                using var ifaceKey = Registry.LocalMachine.OpenSubKey(ifacePath, writable: true);
                if (ifaceKey == null) continue;

                BackupValue(Registry.LocalMachine, ifacePath, "TcpAckFrequency");
                BackupValue(Registry.LocalMachine, ifacePath, "TCPNoDelay");

                ifaceKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                ifaceKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
            }
        }

        LoggingService.Instance.Action("Disabled network throttling index and Nagle's Algorithm on all interfaces.");
    }

    public void RevertNetworkTweaks()
    {
        RestoreValue(Registry.LocalMachine, MMCSSKey, "NetworkThrottlingIndex");

        using var interfacesKey = Registry.LocalMachine.OpenSubKey(TcpParamsKey + @"\Interfaces");
        if (interfacesKey == null) return;

        foreach (var ifaceName in interfacesKey.GetSubKeyNames())
        {
            var ifacePath = TcpParamsKey + $@"\Interfaces\{ifaceName}";
            RestoreValue(Registry.LocalMachine, ifacePath, "TcpAckFrequency");
            RestoreValue(Registry.LocalMachine, ifacePath, "TCPNoDelay");
        }
        LoggingService.Instance.Action("Reverted network/TCP tweaks on all interfaces.");
    }

    // ============================================================
    // 6. Visual Effects (performance mode) + Startup app cleanup
    // ============================================================
    private const string VisualFxKey = @"Control Panel\Desktop";

    public void ApplyPerformanceVisualEffects()
    {
        using var key = Registry.CurrentUser.CreateSubKey(VisualFxKey);
        BackupValue(Registry.CurrentUser, VisualFxKey, "UserPreferencesMask");
        // 90 12 03 80 10 00 00 00 = "Adjust for best performance" preset
        key.SetValue("UserPreferencesMask", new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
        LoggingService.Instance.Action("Applied 'Best Performance' visual effects preset (sign out/in to fully apply).");
    }

    public void RevertPerformanceVisualEffects() =>
        RestoreValue(Registry.CurrentUser, VisualFxKey, "UserPreferencesMask");

    /// <summary>Returns disableable startup entries (name + command) so the UI can let the user pick.</summary>
    public IReadOnlyList<(string Name, string Command)> GetStartupApps()
    {
        var results = new List<(string, string)>();
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (key == null) return results;

        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name) is string cmd)
                results.Add((name, cmd));
        }
        return results;
    }

    public void DisableStartupApp(string name)
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
        var value = key?.GetValue(name) as string;
        if (value == null) return;

        BackupValue(Registry.CurrentUser, runKey, name);
        key!.DeleteValue(name, throwOnMissingValue: false);
        LoggingService.Instance.Action($"Disabled startup app: {name}");
    }

    public void RestoreStartupApp(string name) =>
        RestoreValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", name);

    // ============================================================
    // Restore point gate - call before any Apply* above
    // ============================================================
    public RestorePointResult EnsureRestorePoint(string tweakDescription) =>
        _restorePoints.CreateRestorePoint($"IndoTweaks - before applying: {tweakDescription}");

    // ============================================================
    // Backup/restore plumbing shared by every registry tweak
    // ============================================================
    private void BackupValue(RegistryKey hive, string path, string valueName)
    {
        using var sourceKey = hive.OpenSubKey(path);
        var existing = sourceKey?.GetValue(valueName);

        var backupPath = $@"{BackupRootKey}\{hive.Name.Replace(':', '_')}\{path}";
        using var backupKey = Registry.CurrentUser.CreateSubKey(backupPath);

        if (existing == null)
        {
            backupKey.SetValue(valueName + "__WasMissing", 1, RegistryValueKind.DWord);
        }
        else
        {
            backupKey.SetValue(valueName, existing, sourceKey!.GetValueKind(valueName));
        }
    }

    private void RestoreValue(RegistryKey hive, string path, string valueName)
    {
        var backupPath = $@"{BackupRootKey}\{hive.Name.Replace(':', '_')}\{path}";
        using var backupKey = Registry.CurrentUser.OpenSubKey(backupPath);
        if (backupKey == null)
        {
            LoggingService.Instance.Warn($"No backup found for {path}\\{valueName} - skipping revert.");
            return;
        }

        using var targetKey = hive.OpenSubKey(path, writable: true);
        if (targetKey == null) return;

        if (backupKey.GetValue(valueName + "__WasMissing") != null)
        {
            targetKey.DeleteValue(valueName, throwOnMissingValue: false);
        }
        else
        {
            var backedUpValue = backupKey.GetValue(valueName);
            if (backedUpValue != null)
                targetKey.SetValue(valueName, backedUpValue, backupKey.GetValueKind(valueName));
        }

        LoggingService.Instance.Action($"Reverted {path}\\{valueName} to its original value.");
    }
}
