using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Handles crashes so the widget doesn't just vanish: it saves settings one last time, writes the error to a log file, tells the user where it is, and then exits.
/// </summary>
public static class CrashHandler
{
    /// <summary>
    /// The log starts over once it grows past this, so repeated crashes can't fill the disk.
    /// </summary>
    private const long MaxLogSize = 1024 * 1024;

    private static int _handling;

    /// <summary>
    /// Handles unhandled exceptions on the UI thread and on background threads, such as the monitors' clock tick.
    /// </summary>
    public static void Register(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            // Only matters for a crash while the first one's message is showing, which runs on this thread too: going back to it keeps the message open.
            e.Handled = true;
            HandleAndExit(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleAndExit(e.ExceptionObject as Exception);
    }

    private static void HandleAndExit(Exception exception)
    {
        // Only the first crash is handled. Exiting on another one, like the clock tick failing again a second later, would close the first one's message before it could be read, so the UI thread goes back to showing it and a background thread waits for it to exit.
        if (Interlocked.Exchange(ref _handling, 1) != 0)
        {
            if (Application.Current?.Dispatcher.CheckAccess() != true)
                Thread.Sleep(Timeout.Infinite);

            return;
        }

        // Each of those waiting threads would otherwise make room for a new one while the message is open, so keep the pool from growing.
        ThreadPool.GetMaxThreads(out _, out var completionPortThreads);
        ThreadPool.SetMaxThreads(Environment.ProcessorCount, completionPortThreads);

        TrySaveSettings();
        var logPath = TryWriteLog(exception);
        ShowMessage(logPath);

        // Staying open in an unknown state could leave a widget that looks fine but has stopped updating, so always close.
        Environment.Exit(1);
    }

    private static void TrySaveSettings()
    {
        try
        {
            Settings.Default.Save();
        }
        catch
        {
            // The settings themselves may be what failed.
        }
    }

    /// <summary>
    /// Returns the text of one log entry: when it happened, which version, and the full exception.
    /// The time is written the same way whatever the user's regional format or calendar, so logs from anywhere can be read and compared.
    /// </summary>
    public static string FormatLogEntry(Exception exception, DateTimeOffset time) =>
        $"{time.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)}\r\n" +
        $"Network Monitor {App.VersionText} on {RuntimeInformation.OSDescription.Trim()}, {RuntimeInformation.FrameworkDescription}\r\n" +
        $"{exception}\r\n\r\n";

    /// <summary>
    /// Appends the error to a log next to the app, or in the temp folder if that one can't be written to.
    /// </summary>
    /// <returns>The path of the log that was written, or <c>null</c> if neither could be.</returns>
    private static string TryWriteLog(Exception exception)
    {
        var exePath = Process.GetCurrentProcess().MainModule.FileName;
        var fileName = Path.GetFileNameWithoutExtension(exePath) + ".log";
        var entry = FormatLogEntry(exception, DateTimeOffset.Now);

        foreach (var folder in new[] { Path.GetDirectoryName(exePath), Path.GetTempPath() })
        {
            try
            {
                var path = Path.Combine(folder, fileName);

                if (File.Exists(path) && new FileInfo(path).Length > MaxLogSize)
                    File.Delete(path);

                File.AppendAllText(path, entry);
                return path;
            }
            catch
            {
            }
        }

        return null;
    }

    private static void ShowMessage(string logPath)
    {
        try
        {
            var details = logPath == null
                ? "Please report it at https://github.com/danielchalmers/Network-Monitor/issues."
                : $"Details were saved to:\n{logPath}\n\nPlease report it at https://github.com/danielchalmers/Network-Monitor/issues and include that file.";

            MessageBox.Show(
                "Network Monitor ran into a problem and needs to close.\n\n" + details,
                "Network Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}
