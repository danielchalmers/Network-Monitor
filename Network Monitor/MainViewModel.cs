using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Returns a text summary of every monitor's stats, suitable for pasting into a support ticket or chat.
    /// </summary>
    public string GetOverviewText()
    {
        // The version and time come first so a pasted report says when it was taken and by what.
        var header = $"Network Monitor {App.VersionText}, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}";

        return string.Join(Environment.NewLine + Environment.NewLine,
            new[] { header }.Concat(Monitors.Select(m => m.Details ?? $"{m.Name}: {m.DisplayValue}")));
    }
}
