# IndoTweaks — Fortnite Performance & Optimization Suite

A WPF (.NET 8) desktop app that monitors hardware telemetry, scores your system's
"efficiency" for competitive Fortnite play, and applies/reverts a set of safe,
well-known Windows tweaks — plus a `GameUserSettings.ini` fixer.

## Build & Run

**Just want a working .exe without building anything?** Push this repo to
GitHub and the included workflow (`.github/workflows/build.yml`) will build
a self-contained `IndoTweaks.exe` automatically on every push, and attach it
to a GitHub Release whenever you push a tag like `v1.0.0`:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Check the Actions tab for progress; the `.exe` lands under that tag's
Release page a couple minutes later, with the .NET runtime baked in — end
users don't need anything installed to run it. See `INSTALL.md` for the
end-user-facing version of these instructions.

**Building locally instead:**

Requirements: Visual Studio 2022 (17.8+) or `dotnet` CLI with the
`.NET desktop development` workload, Windows 10 2004+ or Windows 11.

```bash
cd IndoTweaks
dotnet restore
dotnet build -c Release
dotnet run
```

Or to produce the same standalone `.exe` the CI workflow makes:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

**Run as Administrator.** The app.manifest already requests elevation
(`requireAdministrator`), so Visual Studio / the built .exe will prompt UAC on
launch. Without elevation:
- LibreHardwareMonitorLib can't read CPU/GPU temperature or power sensors (ring0
  driver access) — you'll see 0°C and a warning in Logs.
- Registry tweaks under `HKLM` and `NtSetTimerResolution`/`NtSetSystemInformation`
  calls will fail silently or throw.

The Tweaks tab detects elevation (`App.IsRunningAsAdministrator()`) and disables
itself with a banner if not elevated, rather than failing confusingly mid-tweak.

## Project Layout

```
Models/       Plain data types: HardwareSnapshot, TweakItem, ImpactFinding
Services/     All actual logic — hardware polling, registry tweaks, restore
              points, network sampling, Fortnite ini parsing, scoring
ViewModels/   MVVM glue (CommunityToolkit.Mvvm) — one per tab + MainViewModel
Views/        XAML UserControls, one per tab
Controls/     RadialGauge — the arc-style gauge used on the Dashboard
Themes/       DarkTheme.xaml — the Razer/MSI-inspired palette + control styles
Helpers/      Value converters
```

## What each tweak actually does (and how it's reverted)

| Tweak | Mechanism | Reversible? |
|---|---|---|
| GPU Power Preference | `HKCU\...\DirectX\UserGpuPreferences` per-exe value | Yes — prior value backed up under `HKCU\SOFTWARE\IndoTweaks\Backups` |
| Game Mode + HAGS | `HKCU\...\GameBar` + `HKLM\...\GraphicsDrivers\HwSchMode` | Yes (HAGS needs a reboot to re-take effect either direction) |
| CPU Priority | `Process.PriorityClass = High` on the live Fortnite process | Automatic — resets when Fortnite restarts; nothing persists |
| Timer Resolution | `NtSetTimerResolution` (same call multimedia apps use) | Yes — `RevertTimerResolution()` restores the pre-tweak value |
| Network/TCP | `TCPNoDelay`, `TcpAckFrequency`, `NetworkThrottlingIndex` registry values | Yes — same backup/restore mechanism |
| Visual Effects | `UserPreferencesMask` binary value under `Control Panel\Desktop` | Yes |
| Fortnite `GameUserSettings.ini` | Direct file edit | Yes — first edit copies the original to `GameUserSettings.ini.indotweaks_backup` |

Every registry-level tweak's `Apply*()` call is gated behind
`SystemTweakService.EnsureRestorePoint()`, which the ViewModel prompts the user
to confirm before doing anything. Windows throttles restore point creation to
roughly one per 24h — a throttled request is logged and treated as non-fatal,
not a hard failure.

## Known limitations / things to wire up before shipping

- **GPU sensor name matching** in `HardwareMonitorService` matches on substrings
  (`"Core"`, `"Package"`, etc.) because LibreHardwareMonitorLib's sensor naming
  varies slightly between NVIDIA/AMD/Intel. Test against your actual GPU vendor
  and tighten the matches — the current logic favors "some plausible value" over
  "definitely the right sensor" as a starting point.
- **Fortnite exe path** is currently a hardcoded guess
  (`C:\Program Files\Epic Games\Fortnite\...`). Add an Epic Games Launcher
  manifest reader (parses `%ProgramData%\Epic\UnrealEngineLauncher\LauncherInstalled.dat`)
  to find the real install path instead of guessing.
- **FPS/latency numbers** shown per tweak (e.g. "+8 FPS") are rank-ordering
  estimates based on commonly cited community benchmarks, not a measurement of
  *your* system. Labeled as estimates in the UI copy for a reason — don't
  present them as guaranteed.
- **HAGS and some registry changes require a reboot** to fully take effect in
  either direction; the UI should make this explicit rather than implying an
  instant effect (a few `LoggingService` lines already flag this — surface it
  in the card UI too).
- No installer/signing set up — for real distribution you'll want a signed
  MSIX or Squirrel installer so SmartScreen doesn't flag an elevated,
  registry-touching unsigned .exe.

## Safety model

Nothing here does anything irreversible or undocumented:
- Timer resolution uses the same OS API (`NtSetTimerResolution`) games and
  media players already use.
- Standby memory flush uses the same `NtSetSystemInformation` call as the
  well-known `EmptyStandbyList.exe` — it only forces reclaim of already-reclaimable
  cache pages, never active allocations.
- Every registry write has a paired backup entry and revert path.
- A System Restore point is offered before every registry/system-level tweak.
