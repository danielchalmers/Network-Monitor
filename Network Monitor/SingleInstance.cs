using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Network_Monitor;

/// <summary>
/// Keeps to one widget per exe, as DesktopClock does: starting the same exe again brings the running widget into view instead of adding a second one that shares its settings.
/// Copies at other paths keep their own settings, so they still run side by side.
/// </summary>
public static class SingleInstance
{
    private const int ASFW_ANY = -1;

    private static EventWaitHandle _showSignal;

    /// <summary>
    /// Returns whether this is the only widget running from <paramref name="exePath" />.
    /// If another one is, it's asked to show itself, and this process should exit.
    /// </summary>
    public static bool TryClaim(string exePath)
    {
        bool isFirst;

        try
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, GetSignalName(exePath), out isFirst);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            // A copy running as administrator owns a signal this one isn't allowed to open, so it can't be asked to show itself; run as a widget of its own rather than not at all.
            _showSignal = null;
            return true;
        }

        if (isFirst)
            return true;

        // Windows only lets a window come to the front when the user just did something, and here that was starting this process, so pass the permission on.
        AllowSetForegroundWindow(ASFW_ANY);
        _showSignal.Set();
        Release();
        return false;
    }

    /// <summary>
    /// Calls <paramref name="show" /> on a thread pool thread whenever the same exe is started again.
    /// </summary>
    public static void ListenForShowRequests(Action show)
    {
        if (_showSignal != null)
            ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => show(), null, Timeout.Infinite, false);
    }

    /// <summary>
    /// Gives up the claim so a new process of this exe can take over, as when restarting to load repaired settings.
    /// Only valid before <see cref="ListenForShowRequests" />, which holds on to the claim.
    /// </summary>
    public static void Release()
    {
        _showSignal?.Dispose();
        _showSignal = null;
    }

    /// <summary>
    /// Returns the name shared by every process started from <paramref name="exePath" />, whatever case the path was typed in.
    /// </summary>
    public static string GetSignalName(string exePath)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(exePath.ToUpperInvariant()));
        return "Network Monitor-" + BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
