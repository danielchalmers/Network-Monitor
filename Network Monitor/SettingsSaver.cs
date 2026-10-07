using System;
using System.Windows;
using System.Windows.Threading;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Saves settings a second after they last changed, so a crash, a forced restart, or a power cut doesn't lose them.
/// Waiting for changes to settle keeps a drag or a slider from writing the file over and over.
/// </summary>
public static class SettingsSaver
{
    private static DispatcherTimer _timer;

    public static void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => SaveNow();

        Settings.Default.PropertyChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _timer.Stop();
            _timer.Start();
        }));
    }

    /// <summary>
    /// Saves right away, such as when the app is closing.
    /// </summary>
    public static void SaveNow()
    {
        _timer?.Stop();

        try
        {
            Settings.Default.Save();
        }
        catch
        {
            // A locked or read-only settings file shouldn't take the widget down; the next change tries again.
        }
    }
}
