using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoTweaks.Services;

namespace IndoTweaks.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    public ObservableCollection<LogEntry> Entries => LoggingService.Instance.Entries;

    [RelayCommand]
    private void OpenLogFile() => Process.Start(new ProcessStartInfo(LoggingService.Instance.LogFilePath) { UseShellExecute = true });

    [RelayCommand]
    private void ClearLogs() => Entries.Clear();
}
