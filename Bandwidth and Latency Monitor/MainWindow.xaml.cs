using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Settings.Default.MustUpgrade)
        {
            Settings.Default.Upgrade();
            Settings.Default.MustUpgrade = false;
            Settings.Default.Save();
        }

        Settings.Default.PropertyChanged += Settings_PropertyChanged;

        Monitors = new List<Monitor> {
            new LatencyMonitor(Settings.Default.PingHost, Settings.Default.Timeout),
            new DownloadMonitor(),
            new UploadMonitor()
        };
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.Default.RunOnStartup):

                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (Settings.Default.RunOnStartup)
                        key?.SetValue("Bandwidth_and_Latency_Monitor", App.ResourceAssembly.Location);
                    else
                        key?.DeleteValue("Bandwidth_and_Latency_Monitor", false);
                }

                break;
        }
    }

    public IReadOnlyList<Monitor> Monitors { get; }

    private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Scale size based on scroll amount, with one notch on a default PC mouse being a change of 15%.
            var steps = e.Delta / (double)Mouse.MouseWheelDeltaForOneLine;
            var change = Settings.Default.Size * steps * 0.15;
            Settings.Default.Size = (int)Math.Min(Math.Max(Settings.Default.Size + change, 32), 320);
        }
    }

    private void MenuItemCheckForUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://github.com/danielchalmers/Network-Monitor/releases", UseShellExecute = true });
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