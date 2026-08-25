# Installing IndoTweaks — Step-by-Step

Two ways to get this running. Almost everyone should use **Option A**.

---

## Option A — Download the ready-made .exe (recommended, ~2 minutes)

No coding tools, no SDK, nothing to install first. The `.exe` has the .NET
runtime built in.

1. Go to the project's **Releases** page (the maintainer's GitHub repo →
   "Releases" on the right sidebar) and download `IndoTweaks.exe` from the
   latest release.
2. Put it anywhere you like — Desktop, a Tools folder, wherever.
3. Right-click `IndoTweaks.exe` → **Run as administrator**. (Double-clicking
   also works — the app requests elevation automatically via a UAC prompt;
   click **Yes**.)
4. That's it. The dashboard should open with live CPU/GPU/RAM readouts.

**Windows SmartScreen will likely show "Windows protected your PC" the first
time.** This is expected for any freshly-built, unsigned `.exe` — it isn't a
virus detection, just SmartScreen being cautious about anything not yet
widely downloaded. Click **More info** → **Run anyway**.

If your antivirus quarantines it: registry-editing and timer-resolution APIs
are exactly the behavior heuristic scanners flag on *any* unsigned
optimization tool — RivaTuner and MSI Afterburner trigger the same false
positives. Add an exclusion for the file, or restore it from quarantine after
confirming it's the file you downloaded.

Skip to **First-run checklist** below once it's open.

---

## Option B — Build it yourself from source (advanced / for developers)

Use this if you want to read every line before running it, or you're
modifying the code.

### Prerequisites
- Windows 10 (2004+) or Windows 11, 64-bit
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the SDK, not
  just the Runtime)

### Steps

1. Extract `IndoTweaks.zip` somewhere simple, e.g. `C:\Dev\IndoTweaks`
   (avoid Desktop/OneDrive-synced folders).
2. Shift + right-click inside the folder → **"Open PowerShell window here."**
3. Run:
   ```
   dotnet restore
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
   ```
   This produces the exact same kind of standalone `.exe` as Option A, at
   `publish\IndoTweaks.exe`.
4. Run it as Administrator (right-click → Run as administrator).

Prefer a plain debug build instead of a standalone exe? `dotnet run -c
Release` works too, but requires the .NET 8 **Runtime** to already be
installed on any machine you copy it to — the publish command above is
what makes it portable to a PC with nothing installed.

---

## First-run checklist

1. **Dashboard tab** — confirm you're seeing real (non-zero) CPU/GPU
   temperatures. If they read 0°C, you're not running elevated — close the
   app, right-click → Run as administrator.
2. **Tweaks tab** — click **Rescan All**. The banner at the top warns you if
   you're not admin.
3. **Fortnite Settings Fixer tab** — auto-detects your `GameUserSettings.ini`.
   If it says "not found," click **Browse...** and paste this into the file
   picker's address bar:
   ```
   %LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini
   ```
4. Before applying any tweak, IndoTweaks offers to create a **System Restore
   point** — click **Yes** the first time so you have a safety net.

---

## Troubleshooting

**"Windows protected your PC" (SmartScreen)**
Expected for an unsigned exe. Click **More info** → **Run anyway**.

**Antivirus flags or quarantines the exe**
Same cause as SmartScreen — unsigned + registry/system APIs. Add an
exclusion or restore from quarantine.

**CPU/GPU temps show 0°C**
You're not running elevated. Check the Logs tab for a confirming warning.

**"Fortnite isn't currently running" on the CPU Priority tweak**
That tweak targets the live game process — launch Fortnite first, then
apply it while the game is running.

**GameUserSettings.ini not found**
It's only created after Fortnite has been launched at least once. Launch
the game, quit to desktop, then retry.

**(Option B only) Build fails / `dotnet` not recognized**
Restart your PC after installing the SDK — the installer updates PATH, which
sometimes needs a restart to take effect.

---

## Uninstalling / cleaning up

IndoTweaks doesn't install anything system-wide — it's a single file (or
folder, if you built from source). To remove it cleanly:

1. In the **Tweaks** tab, click **Revert** on any tweak marked "Applied."
2. In the **Fortnite Settings Fixer** tab, click **Restore Original** if you
   applied the competitive preset.
3. Delete `IndoTweaks.exe` (or the whole project folder).

Registry backups live under `HKEY_CURRENT_USER\SOFTWARE\IndoTweaks\Backups` —
reverting each tweak from the app cleans up its own backup entries.
