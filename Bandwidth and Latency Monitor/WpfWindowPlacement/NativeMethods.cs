using System;
using System.Runtime.InteropServices;

namespace WpfWindowPlacement;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, out WindowPlacement lpwndpl);
}