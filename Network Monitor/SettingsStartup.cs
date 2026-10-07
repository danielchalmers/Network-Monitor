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
    /// Brings in another copy's settings if this copy has none, upgrades settings from an older version, and repairs a settings file that can't be read.
    /// </summary>
    /// <returns>False when the app restarted itself to load repaired settings, so this process should exit.</returns>
    public static bool Prepare(string[] args)
    {
        var userConfigPath = TryGetUserConfigPath();

        try
        {
            if (userConfigPath != null)
                TryImport(userConfigPath);

            if (Settings.Default.MustUpgrade)
            {
                Settings.Default.Upgrade();
                Settings.Default.MustUpgrade = false;
                Settings.Default.Save();
            }

            return true;
        }
        catch (ConfigurationErrorsException ex) when (!args.Contains(RepairedArgument) && TryDeleteUnreadable(ex, userConfigPath))
        {
            // The settings already failed to load in this process and can't be loaded again, so start over in a fresh one.
            Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, RepairedArgument) { UseShellExecute = false });
            return false;
        }
    }

    /// <summary>
    /// Returns where this copy keeps its settings for the current version, or null if the existing file can't even be opened, which <see cref="TryDeleteUnreadable" /> deals with.
    /// </summary>
    private static string TryGetUserConfigPath()
    {
        try
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
        }
        catch (ConfigurationErrorsException)
        {
            return null;
        }
    }

    private static void TryImport(string userConfigPath)
    {
        try
        {
            UserConfigImport.ImportIfNewCopy(userConfigPath, includePlacement: !IsAnotherCopyRunning());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // Starting from defaults is better than not starting.
        }
    }

    /// <summary>
    /// Deletes the settings file that failed to load, as long as it belongs to this copy (the current file or the older one <see cref="Settings.Upgrade" /> reads).
    /// A file cut short by a power loss would otherwise stop the app from starting every time, and reinstalling doesn't remove it.
    /// </summary>
    private static bool TryDeleteUnreadable(ConfigurationErrorsException exception, string userConfigPath)
    {
        var failedFile = (exception.InnerException as ConfigurationErrorsException)?.Filename ?? exception.Filename;

        if (string.IsNullOrEmpty(failedFile) || !string.Equals(Path.GetFileName(failedFile), "user.config", StringComparison.OrdinalIgnoreCase))
            return false;

        // When the current file can't be opened its path isn't known up front, but the failing file names it.
        var copyFolder = Path.GetDirectoryName(Path.GetDirectoryName(userConfigPath ?? failedFile));

        if (!IsInFolder(failedFile, copyFolder))
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

    /// <summary>
    /// Returns whether <paramref name="path" /> is inside <paramref name="folder" />.
    /// </summary>
    public static bool IsInFolder(string path, string folder) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether another copy of Network Monitor is running, at any path.
    /// </summary>
    private static bool IsAnotherCopyRunning()
    {
        var currentId = Process.GetCurrentProcess().Id;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                // Downloads can be renamed (like "Network.Monitor (1)"), so look at what the exe says it is rather than its name.
                if (process.Id != currentId &&
                    process.ProcessName.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    process.MainModule?.FileVersionInfo.ProductName == "Network Monitor")
                    return true;
            }
            catch
            {
                // Processes we can't inspect, like elevated ones, aren't ours.
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}
