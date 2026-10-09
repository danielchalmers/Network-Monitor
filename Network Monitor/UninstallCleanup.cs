using System.IO;

namespace Network_Monitor;

/// <summary>
/// Removes what the app adds outside the installer's knowledge, so uninstalling doesn't leave a startup entry for an exe that's gone.
/// Settings stay, so reinstalling, or switching to the portable exe, picks up where it left off.
/// </summary>
public static class UninstallCleanup
{
    /// <summary>
    /// The installer starts the exe with this when it's uninstalled, but not when a newer version replaces it.
    /// </summary>
    public const string Argument = "--uninstall";

    /// <summary>
    /// Stops Windows from starting <paramref name="exePath" /> at sign-in and deletes its crash log.
    /// The uninstall waits for this, so nothing here may fail or show anything.
    /// </summary>
    public static void Run(string exePath)
    {
        try
        {
            StartupRegistration.DisableFor(exePath);
        }
        catch
        {
            // A policy can make the Run key read-only.
        }

        try
        {
            File.Delete(CrashHandler.GetLogPath(Path.GetDirectoryName(exePath), exePath));
        }
        catch
        {
            // A log that can't be deleted only keeps the install folder around.
        }
    }
}
