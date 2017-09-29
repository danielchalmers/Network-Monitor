using System;
using System.Runtime.InteropServices;

namespace Network_Monitor
{
    internal static class NativeMethods
    {
        internal const int SW_SHOWNORMAL = 1;
        internal const int SW_SHOWMINIMIZED = 2;

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref Placement.WindowPlacement lpwndpl);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, out Placement.WindowPlacement lpwndpl);
    }
}