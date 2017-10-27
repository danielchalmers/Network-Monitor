using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Network_Monitor.Placement
{
    /// <summary>
    /// <para><see cref="WindowPlacement"/> related functions.</para>
    /// <para>See <see cref="WindowPlacementProperties"/> for use in XAML.</para>
    /// </summary>
    // https://github.com/danielchalmers/WpfWindowPlacement.
    public static class WindowPlacementFunctions
    {
        /// <summary>
        /// Retrieves the show state and the restored, minimized, and maximized positions of the specified window.
        /// </summary>
        /// <param name="windowHandle">A handle to the window.</param>
        public static WindowPlacement GetPlacement(IntPtr windowHandle)
        {
            var placement = new WindowPlacement
            {
                // The length member of WindowPlacement must be set to sizeof(WindowPlacement).
                // If this member is not set correctly, the P/Invoke function returns false.
                Length = Marshal.SizeOf(typeof(WindowPlacement))
            };

            // P/Invoke win32 method to get the window's placement.
            NativeMethods.GetWindowPlacement(windowHandle, out placement);

            return placement;
        }

        /// <summary>
        /// Retrieves the show state and the restored, minimized, and maximized positions of the specified window.
        /// </summary>
        /// <param name="window">A window.</param>
        public static WindowPlacement GetPlacement(this Window window)
        {
            return GetPlacement(new WindowInteropHelper(window).Handle);
        }

        /// <summary>
        /// Sets the show state and the restored, minimized, and maximized positions of the specified window.
        /// </summary>
        /// <param name="windowHandle">A handle to the window.</param>
        /// <param name="placement">A <see cref="WindowPlacement"/> structure that specifies the new show state and window positions.</param>
        public static void SetPlacement(IntPtr windowHandle, WindowPlacement placement)
        {
            // Don't continue if placement is default.
            if (placement.Equals(default(WindowPlacement)))
            {
                return;
            }

            // The length member of WindowPlacement must be set to sizeof(WindowPlacement).
            // If this member is not set correctly, the P/Invoke function returns false.
            placement.Length = Marshal.SizeOf(typeof(WindowPlacement));

            // Restore window to normal state if minimized.
            if (placement.ShowCommand == WindowPlacementShowCommand.ShowMinimized)
            {
                placement.ShowCommand = WindowPlacementShowCommand.ShowNormal;
            }

            // P/Invoke win32 method to set the window's placement.
            NativeMethods.SetWindowPlacement(windowHandle, ref placement);
        }

        /// <summary>
        /// Sets the show state and the restored, minimized, and maximized positions of the specified window.
        /// </summary>
        /// <param name="window">A window.</param>
        /// <param name="placement">A <see cref="WindowPlacement"/> structure that specifies the new show state and window positions.</param>
        public static void SetPlacement(this Window window, WindowPlacement placement)
        {
            SetPlacement(new WindowInteropHelper(window).Handle, placement);
        }
    }
}