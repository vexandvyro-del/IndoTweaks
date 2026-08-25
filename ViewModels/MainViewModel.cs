using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoTweaks.Models;
using IndoTweaks.Services;

namespace IndoTweaks.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly EfficiencyScoreService _efficiencyService = new();
    private readonly FortniteConfigService _fortniteConfigService = new();

    public DashboardViewModel Dashboard { get; }
    public TweaksViewModel Tweaks { get; }
    public FortniteSettingsViewModel FortniteSettings { get; }
    public LogsViewModel Logs { get; }

    [ObservableProperty] private int _efficiencyScore = 100;
    [ObservableProperty] private int _totalFpsPenalty;
    [ObservableProperty] private int _totalLatencyPenaltyMs;
    [ObservableProperty] private string _activeTab = "Dashboard";

    public MainViewModel()
    {
        Dashboard = new DashboardViewModel();
        Tweaks = new TweaksViewModel();
        FortniteSettings = new FortniteSettingsViewModel();
        Logs = new LogsViewModel();

        RecomputeEfficiencyScore();
    }

    [RelayCommand]
    private void NavigateTo(string tab) => ActiveTab = tab;

    [RelayCommand]
    private void RecomputeEfficiencyScore()
    {
        Tweaks.RefreshStatesCommand.Execute(null);
        var mismatches = FortniteSettings.ConfigFound
            ? _fortniteConfigService.FindMismatches(FortniteSettings.ConfigPath)
            : new List<(FortniteSettingDefinition Def, string CurrentValue)>();

        var report = _efficiencyService.BuildReport(Tweaks.Tweaks, mismatches);
        EfficiencyScore = report.ScorePercent;
        TotalFpsPenalty = report.TotalFpsPenalty;
        TotalLatencyPenaltyMs = report.TotalLatencyPenaltyMs;
    }
}
