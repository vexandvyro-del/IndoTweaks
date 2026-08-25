using System;
using System.Security.Principal;
using System.Windows;
using IndoTweaks.Services;

namespace IndoTweaks;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            LoggingService.Instance.Error("Unhandled exception", args.ExceptionObject as Exception);
            MessageBox.Show(
                "IndoTweaks hit an unexpected error and needs to close. Details were written to the Logs tab / log file.",
                "IndoTweaks - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            LoggingService.Instance.Error("Dispatcher exception", args.Exception);
            MessageBox.Show(
                $"Something went wrong:\n{args.Exception.Message}\n\nSee Logs for details.",
                "IndoTweaks - Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true; // keep the app alive for non-fatal UI errors
        };

        if (!IsRunningAsAdministrator())
        {
            MessageBox.Show(
                "IndoTweaks needs to run as Administrator to read hardware sensors reliably and apply " +
                "system tweaks (registry, timer resolution, network stack).\n\n" +
                "The app will still open, but the Tweaks tab will be disabled until you restart as Admin.",
                "IndoTweaks - Limited Mode", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
