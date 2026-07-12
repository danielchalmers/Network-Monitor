using System;
using System.Collections.Generic;
using System.Linq;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// View model for <see cref="MainWindow" />.
/// </summary>
public class MainViewModel : ObservableObject
{
    private bool _updatesPaused;

    public MainViewModel()
    {
        Monitors = new List<Monitor>
        {
            new LatencyMonitor(Settings.Default.PingHost, Settings.Default.Timeout),
            new DownloadMonitor(),
            new UploadMonitor()
        };
    }

    public IReadOnlyList<Monitor> Monitors { get; }

    /// <summary>
    /// Whether the monitors should hold their displayed values instead of refreshing, such as while the window is being dragged with the mouse.
    /// </summary>
    public bool UpdatesPaused
    {
        get => _updatesPaused;
        set
        {
            if (!Set(ref _updatesPaused, value))
                return;

            foreach (var monitor in Monitors)
                monitor.IsPaused = value;
        }
    }

    /// <summary>
    /// Returns a text summary of every monitor's current value.
    /// </summary>
    public string GetOverviewText()
    {
        var longestMonitorName = Monitors.Max(m => m.Name.Length);

        return string.Join(Environment.NewLine, Monitors.Select(m => $"{m.Name.PadRight(longestMonitorName)}: {m.DisplayValue}"));
    }
}
