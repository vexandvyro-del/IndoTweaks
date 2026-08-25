using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoTweaks.Services;

namespace IndoTweaks.ViewModels;

public partial class FortniteSettingsViewModel : ObservableObject
{
    private readonly FortniteConfigService _configService = new();

    [ObservableProperty]
    private string _configPath = FortniteConfigService.GetDefaultConfigPath();

    [ObservableProperty]
    private bool _configFound = File.Exists(FortniteConfigService.GetDefaultConfigPath());

    public ObservableCollection<MismatchRow> Mismatches { get; } = new();

    public FortniteSettingsViewModel()
    {
        Rescan();
    }

    public sealed partial class MismatchRow : ObservableObject
    {
        public required string DisplayName { get; init; }
        public required string CurrentValue { get; init; }
        public required string RecommendedValue { get; init; }
        public required string Reason { get; init; }
        [ObservableProperty] private bool _isSelected = true;
        public required FortniteSettingDefinition Definition { get; init; }
    }

    [RelayCommand]
    private void BrowseForConfig()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "GameUserSettings.ini|GameUserSettings.ini|All files|*.*",
            Title = "Locate GameUserSettings.ini"
        };
        if (dialog.ShowDialog() == true)
        {
            ConfigPath = dialog.FileName;
            ConfigFound = true;
            Rescan();
        }
    }

    [RelayCommand]
    private void Rescan()
    {
        Mismatches.Clear();
        ConfigFound = File.Exists(ConfigPath);
        if (!ConfigFound) return;

        foreach (var (def, current) in _configService.FindMismatches(ConfigPath))
        {
            Mismatches.Add(new MismatchRow
            {
                DisplayName = def.DisplayName,
                CurrentValue = current,
                RecommendedValue = def.RecommendedValue,
                Reason = def.Reason,
                Definition = def,
            });
        }
    }

    [RelayCommand]
    private void ApplySelected()
    {
        if (IsFortniteRunning())
        {
            MessageBox.Show(
                "Fortnite is currently running. Close it first - the game rewrites GameUserSettings.ini on exit " +
                "and would overwrite these changes.",
                "Close Fortnite First", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedDefs = Mismatches.Where(m => m.IsSelected).Select(m => m.Definition);
        try
        {
            _configService.ApplyRecommended(ConfigPath, selectedDefs);
            MessageBox.Show("Applied selected competitive settings. Launch Fortnite to see them take effect.",
                "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            Rescan();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to apply settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RestoreOriginal()
    {
        _configService.RestoreBackup(ConfigPath);
        Rescan();
    }

    private static bool IsFortniteRunning() =>
        System.Diagnostics.Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length > 0;
}
