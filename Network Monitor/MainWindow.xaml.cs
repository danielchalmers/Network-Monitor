using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _updateTimer;

    public MainWindow()
    {
        InitializeComponent();

        if (Settings.Default.MustUpgrade)
        {
            Settings.Default.Upgrade();
            Settings.Default.MustUpgrade = false;
            Settings.Default.Save();
        }

        Monitors = new List<Monitor> {
            new LatencyMonitor(),
            new DownloadMonitor(),
            new UploadMonitor()
        };

        _updateTimer = new(DispatcherPriority.Normal);
        _updateTimer.Interval = Settings.Default.Interval;
        _updateTimer.Tick += async (_, _) => await UpdateAllMonitors();
    }

    public IReadOnlyList<Monitor> Monitors { get; }

    private async void Window_SourceInitialized(object sender, EventArgs e)
    {
        await UpdateAllMonitors();

        _updateTimer.Start();
    }

    private async Task UpdateAllMonitors()
    {
        foreach (var monitor in Monitors)
        {
            try
            {
                await monitor.UpdateAsync();
            }
            catch
            {
            }
        }
    }

    private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var newSize = Settings.Default.Size + e.Delta;

            newSize = Math.Max(newSize, 48);
            newSize = Math.Min(newSize, 240);

            Settings.Default.Size = newSize;
        }
    }

    private void MenuItemExit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_OnClosed(object sender, EventArgs e)
    {
        Settings.Default.Save();
    }
}