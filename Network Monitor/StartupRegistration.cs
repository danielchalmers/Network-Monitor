using System;
using System.IO;
using Microsoft.Win32;

namespace Network_Monitor;

/// <summary>
/// Starts the app when the user signs in to Windows, through the per-user Run key that Task Manager's Startup apps lists.
/// The registry is the source of truth, so the menu always shows what Windows will actually do.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Network_Monitor";

    /// <summary>
    /// Returns whether Windows will start <paramref name="exePath" /> at sign-in: the Run value names it and Task Manager hasn't disabled it.
    /// </summary>
    public static bool IsEnabled(string exePath)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        using var approvedKey = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);

        return IsSamePath(runKey?.GetValue(ValueName) as string, exePath) && IsApproved(approvedKey?.GetValue(ValueName) as byte[]);
    }

    /// <summary>
    /// Starts <paramref name="exePath" /> at sign-in, undoing a Task Manager "Disable" since this is an explicit request to start it.
    /// </summary>
    public static void Enable(string exePath)
    {
        using (var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            runKey.SetValue(ValueName, Quote(exePath));

        using var approvedKey = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approvedKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Stops starting the app at sign-in.
    /// </summary>
    public static void Disable()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Points the Run value at <paramref name="exePath" /> when the exe it names was deleted, such as after the user deleted an old portable download and kept the new one.
    /// Also quotes a path that was saved without quotes by older versions, which is unreliable for paths with spaces.
    /// A Run value for another copy that still exists, or might just be unreachable right now, is left alone.
    /// </summary>
    public static void RepairOnLaunch(string exePath)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (runKey?.GetValue(ValueName) is not string value)
            return;

        if ((IsSamePath(value, exePath) || WasDeleted(GetPath(value))) && value != Quote(exePath))
            runKey.SetValue(ValueName, Quote(exePath));
    }

    /// <summary>
    /// Returns whether the exe at <paramref name="path" /> was deleted from a folder on this PC that's still there.
    /// An uninstalled copy's folder is gone with it, so its leftover entry isn't taken over by a copy the user only tried once.
    /// An exe on a network share or removable drive may only be unreachable right now, and checking a share can stall startup, so those are never treated as deleted.
    /// </summary>
    public static bool WasDeleted(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\") || !Path.IsPathRooted(path))
                return false;

            var drive = new DriveInfo(Path.GetPathRoot(path));

            return drive.DriveType == DriveType.Fixed && drive.IsReady && Directory.Exists(Path.GetDirectoryName(path)) && !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the exe path from a Run value, which may or may not be quoted.
    /// </summary>
    public static string GetPath(string runValue)
    {
        if (string.IsNullOrWhiteSpace(runValue))
            return null;

        var value = runValue.Trim();

        if (!value.StartsWith("\""))
            return value;

        var end = value.IndexOf('"', 1);
        return end > 0 ? value.Substring(1, end - 1) : value.Trim('"');
    }

    /// <summary>
    /// Returns whether a Run value names <paramref name="exePath" />.
    /// </summary>
    public static bool IsSamePath(string runValue, string exePath) =>
        GetPath(runValue) is string path && string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether Task Manager's Startup apps leaves the entry enabled, which it records in the first byte of its StartupApproved value: even means enabled, odd means disabled.
    /// </summary>
    public static bool IsApproved(byte[] startupApprovedValue) =>
        startupApprovedValue is not { Length: > 0 } || startupApprovedValue[0] % 2 == 0;

    private static string Quote(string path) => "\"" + path + "\"";
}
