using System.Windows;
using IndoTweaks.ViewModels;

namespace IndoTweaks;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void NavDashboard_Checked(object sender, RoutedEventArgs e) => ShowTab(DashboardViewHost);
    private void NavTweaks_Checked(object sender, RoutedEventArgs e) => ShowTab(TweaksViewHost);
    private void NavFortnite_Checked(object sender, RoutedEventArgs e) => ShowTab(FortniteViewHost);
    private void NavLogs_Checked(object sender, RoutedEventArgs e) => ShowTab(LogsViewHost);

    private void ShowTab(UIElement toShow)
    {
        DashboardViewHost.Visibility = Visibility.Collapsed;
        TweaksViewHost.Visibility = Visibility.Collapsed;
        FortniteViewHost.Visibility = Visibility.Collapsed;
        LogsViewHost.Visibility = Visibility.Collapsed;
        toShow.Visibility = Visibility.Visible;
    }

    // ---- Custom title bar (WindowStyle="None") caption button handlers ----
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
