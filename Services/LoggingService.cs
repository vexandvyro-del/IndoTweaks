using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace IndoTweaks.Services;

public enum LogLevel { Info, Warn, Error, Action }

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message);

/// <summary>
/// Central logging sink. Every tweak apply/revert, hardware read failure,
/// and unhandled exception funnels through here so the Logs tab always
/// reflects exactly what the app has done to the system (audit trail).
/// </summary>
public sealed class LoggingService
{
    private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());
    public static LoggingService Instance => _instance.Value;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    private readonly string _logFilePath;
    private readonly object _fileLock = new();

    private LoggingService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IndoTweaks", "Logs");
        Directory.CreateDirectory(dir);
        _logFilePath = Path.Combine(dir, $"indotweaks_{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Action(string message) => Write(LogLevel.Action, message);

    public void Error(string message, Exception? ex = null) =>
        Write(LogLevel.Error, ex is null ? message : $"{message}: {ex.Message}");

    private void Write(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);

        void addToUi()
        {
            Entries.Insert(0, entry);
            // Keep the in-memory list bounded; full history still lives in the file.
            while (Entries.Count > 2000) Entries.RemoveAt(Entries.Count - 1);
        }

        if (Application.Current?.Dispatcher.CheckAccess() == false)
            Application.Current.Dispatcher.Invoke(addToUi);
        else
            addToUi();

        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(_logFilePath,
                    $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
            catch { /* best-effort file logging; never crash the app over a log write */ }
        }
    }

    public string LogFilePath => _logFilePath;
}
