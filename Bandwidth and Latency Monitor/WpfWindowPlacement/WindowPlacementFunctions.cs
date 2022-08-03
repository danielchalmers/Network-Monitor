using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WpfWindowPlacement;

/// <summary>
/// <para><see cref="WindowPlacement"/> related functions.</para>
/// <para>See <see cref="WindowPlacementProperties"/> for use in XAML.</para>
/// </summary>
public static class WindowPlacementFunctions
{
    /// <summary>
    /// Retrieves the show state and the restored, minimized, and maximized positions of the specified window.
    /// </summary>
    /// <param name="windowHandle">The handle to the window.</param>
    public static WindowPlacement GetPlacement(IntPtr windowHandle)
    {
        var placement = new WindowPlacement
        {
            // The length member must be set correctly or the P/Invoke function returns false.
            Length = Marshal.SizeOf(typeof(WindowPlacement))
        };

        NativeMethods.GetWindowPlacement(windowHandle, out placement);

        return placement;
    }

    /// <summary>
    /// Retrieves the show state and the restored, minimized, and maximized positions of the specified window.
    /// </summary>
    /// <param name="window">The window.</param>
    public static WindowPlacement GetPlacement(this Window window) =>
        GetPlacement(new WindowInteropHelper(window).Handle);

    /// <summary>
    /// Sets the show state and the restored, minimized, and maximized positions of the specified window.
    /// </summary>
    /// <param name="windowHandle">The handle to the window.</param>
    /// <param name="placement">A <see cref="WindowPlacement"/> structure that specifies the new show state and window positions.</param>
    public static void SetPlacement(IntPtr windowHandle, WindowPlacement placement)
    {
        if (placement.Equals(default(WindowPlacement)))
            return;

        // Restore window to normal state if minimized.
        if (placement.ShowCommand == WindowPlacementShowCommand.ShowMinimized)
            placement.ShowCommand = WindowPlacementShowCommand.ShowNormal;

        // Length must be set correctly or the P/Invoke function will return false.
        placement.Length = Marshal.SizeOf(typeof(WindowPlacement));

        // P/Invoke win32 method to set the window's placement.
        NativeMethods.SetWindowPlacement(windowHandle, ref placement);
    }

    /// <summary>
    /// Sets the show state and the restored, minimized, and maximized positions of the specified window.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="placement">A <see cref="WindowPlacement"/> structure that specifies the new show state and window positions.</param>
    public static void SetPlacement(this Window window, WindowPlacement placement) =>
        SetPlacement(new WindowInteropHelper(window).Handle, placement);
}