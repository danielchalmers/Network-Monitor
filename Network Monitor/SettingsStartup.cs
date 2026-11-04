using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Gets the settings ready before anything reads them.
/// </summary>
public static class SettingsStartup
{
    private const string RepairedArgument = "--repaired";

    /// <summary>
    /// Upgrades settings from an older version, and repairs a settings file that can't be read.
    /// </summary>
    /// <returns>False when the app restarted itself to load repaired settings, so this process should exit.</returns>
    public static bool Prepare(string[] args)
    {
        try
        {
            if (Settings.Default.MustUpgrade)
            {
                Settings.Default.Upgrade();
                Settings.Default.MustUpgrade = false;
                Settings.Default.Save();
            }

            return true;
        }
        catch (ConfigurationErrorsException ex) when (!args.Contains(RepairedArgument) && TryDeleteUnreadable(ex))
        {
            // The settings already failed to load in this process and can't be loaded again, so start over in a fresh one.
            SingleInstance.Release();
            Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, RepairedArgument) { UseShellExecute = false });
            return false;
        }
    }

    /// <summary>
    /// Deletes the settings file that failed to load, which is this copy's current one or the older one <see cref="Settings.Upgrade" /> reads.
    /// A file cut short by a power loss would otherwise stop the app from starting every time, and reinstalling doesn't remove it.
    /// </summary>
    private static bool TryDeleteUnreadable(ConfigurationErrorsException exception)
    {
        var failedFile = (exception.InnerException as ConfigurationErrorsException)?.Filename ?? exception.Filename;

        if (string.IsNullOrEmpty(failedFile) || !string.Equals(Path.GetFileName(failedFile), "user.config", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            File.Delete(failedFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
