using System.IO;
using System.Text;

namespace IndoTweaks.Services;

public sealed record FortniteSettingDefinition(
    string Section,
    string Key,
    string RecommendedValue,
    string DisplayName,
    string Reason
);

/// <summary>
/// Reads/writes Fortnite's GameUserSettings.ini directly. The file is a standard
/// Unreal Engine INI (section headers + key=value), so we parse it generically
/// rather than depending on a fixed schema - this survives Epic adding new keys
/// between seasons.
/// </summary>
public sealed class FortniteConfigService
{
    // Default path: %LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini
    public static string GetDefaultConfigPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");

    /// <summary>
    /// Competitive preset recommendations. Kept as data so tuning them doesn't require
    /// touching parsing logic - update this list as Epic renames/adds settings.
    /// </summary>
    public static readonly IReadOnlyList<FortniteSettingDefinition> RecommendedSettings = new[]
    {
        new FortniteSettingDefinition("ScalabilityGroups", "sg.ResolutionQuality", "100",
            "Resolution Scale", "Native resolution avoids upscaling blur without extra GPU cost at competitive settings."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "bUseVSync", "False",
            "VSync", "VSync adds a frame (or more) of input delay for no competitive benefit."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "PostProcessingQuality", "0",
            "Post-Processing", "Off removes motion blur, bloom, and DoF - all reduce clarity of enemies."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "MotionBlur", "0",
            "Motion Blur", "Off. Motion blur hurts target tracking with zero competitive upside."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "ViewDistanceQuality", "3",
            "View Distance", "Epic/high view distance matters for spotting players at range in BR."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "ShadowQuality", "0",
            "Shadows", "Off. Shadows are the single biggest FPS cost for near-zero visual/competitive value."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "TextureQuality", "1",
            "Textures", "Low/Medium - minimal FPS cost but keep at least Medium for texture streaming stability."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "EffectsQuality", "0",
            "Effects", "Off. Reduces visual clutter from abilities/explosions that can obscure enemies."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "bUseLowLatencyMode", "True",
            "Low Latency Mode (NVIDIA Reflex)", "On. Reduces render-queue input latency, especially with uncapped FPS."),
        new FortniteSettingDefinition("/Script/FortniteGame.FortGameUserSettings", "FrameRateLimit", "0",
            "Frame Rate Limit", "Uncapped (0) unless your monitor's refresh rate dictates a specific cap."),
    };

    public Dictionary<string, Dictionary<string, string>> ReadAllSections(string configPath)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(configPath)) return result;

        string? currentSection = null;
        foreach (var rawLine in File.ReadLines(configPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("#")) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1];
                result.TryAdd(currentSection, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                continue;
            }

            var eqIdx = line.IndexOf('=');
            if (currentSection == null || eqIdx < 0) continue;

            var key = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            result[currentSection][key] = value;
        }
        return result;
    }

    /// <summary>Compares current file contents against RecommendedSettings and returns mismatches.</summary>
    public List<(FortniteSettingDefinition Def, string CurrentValue)> FindMismatches(string configPath)
    {
        var sections = ReadAllSections(configPath);
        var mismatches = new List<(FortniteSettingDefinition, string)>();

        foreach (var def in RecommendedSettings)
        {
            var currentValue = sections.TryGetValue(def.Section, out var kv) && kv.TryGetValue(def.Key, out var v)
                ? v
                : "(not set)";

            if (!string.Equals(currentValue, def.RecommendedValue, StringComparison.OrdinalIgnoreCase))
                mismatches.Add((def, currentValue));
        }
        return mismatches;
    }

    /// <summary>
    /// Applies all recommended settings. Fortnite must be closed - the client rewrites
    /// this file on exit, which would otherwise clobber our changes. Always backs up
    /// the original file next to it as GameUserSettings.ini.indotweaks_backup first.
    /// </summary>
    public void ApplyRecommended(string configPath, IEnumerable<FortniteSettingDefinition>? subset = null)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("GameUserSettings.ini not found. Launch Fortnite at least once first.", configPath);

        var backupPath = configPath + ".indotweaks_backup";
        if (!File.Exists(backupPath))
            File.Copy(configPath, backupPath);

        var sections = ReadAllSections(configPath);
        var toApply = subset ?? RecommendedSettings;

        foreach (var def in toApply)
        {
            if (!sections.TryGetValue(def.Section, out var kv))
            {
                kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[def.Section] = kv;
            }
            kv[def.Key] = def.RecommendedValue;
        }

        WriteSections(configPath, sections);
        LoggingService.Instance.Action($"Applied {toApply.Count()} recommended Fortnite settings to {configPath}");
    }

    public void RestoreBackup(string configPath)
    {
        var backupPath = configPath + ".indotweaks_backup";
        if (!File.Exists(backupPath))
        {
            LoggingService.Instance.Warn("No IndoTweaks backup of GameUserSettings.ini found.");
            return;
        }
        File.Copy(backupPath, configPath, overwrite: true);
        LoggingService.Instance.Action("Restored GameUserSettings.ini from IndoTweaks backup.");
    }

    private void WriteSections(string configPath, Dictionary<string, Dictionary<string, string>> sections)
    {
        var sb = new StringBuilder();
        foreach (var (section, kv) in sections)
        {
            sb.AppendLine($"[{section}]");
            foreach (var (key, value) in kv)
                sb.AppendLine($"{key}={value}");
            sb.AppendLine();
        }
        File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
    }
}
