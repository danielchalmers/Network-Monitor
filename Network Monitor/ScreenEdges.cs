using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Network_Monitor;

/// <summary>
/// Keeps the widget against the screen edges it's parked near when its size changes, and on its monitor.
/// Windows grow from their top-left corner, so a widget parked in a bottom or right corner would otherwise spill off the screen whenever it got bigger.
/// </summary>
public static class ScreenEdges
{
    /// <summary>
    /// Which edges of the window stay where they are when it changes size.
    /// </summary>
    [Flags]
    public enum FixedEdges
    {
        TopLeft = 0,
        Right = 1,
        Bottom = 2,
    }

    /// <summary>
    /// Returns the edges to keep fixed for a window at <paramref name="bounds" />: whichever are nearer the sides of <paramref name="screen" />, decided by which half of it the window is in.
    /// This is decided once per move rather than on every resize, so a window that grows across the middle of the screen doesn't switch edges partway and drift.
    /// </summary>
    public static FixedEdges GetFixedEdges(Rect bounds, Rect screen)
    {
        var edges = FixedEdges.TopLeft;

        if (bounds.Left + (bounds.Width / 2) > screen.Left + (screen.Width / 2))
            edges |= FixedEdges.Right;

        if (bounds.Top + (bounds.Height / 2) > screen.Top + (screen.Height / 2))
            edges |= FixedEdges.Bottom;

        return edges;
    }

    /// <summary>
    /// Returns where a window that was at <paramref name="oldBounds" /> should go once it's <paramref name="newSize" />.
    /// It keeps <paramref name="edges" /> where they were, then keeps the window on <paramref name="screen" /> where it fits.
    /// </summary>
    public static Point GetPositionAfterResize(Rect oldBounds, Size newSize, Rect screen, FixedEdges edges)
    {
        var x = edges.HasFlag(FixedEdges.Right) ? oldBounds.Right - newSize.Width : oldBounds.Left;
        var y = edges.HasFlag(FixedEdges.Bottom) ? oldBounds.Bottom - newSize.Height : oldBounds.Top;

        return KeepOnScreen(new Rect(new Point(x, y), newSize), screen);
    }

    /// <summary>
    /// Returns the position that moves <paramref name="bounds" /> fully onto <paramref name="screen" />, or as much as fits.
    /// </summary>
    public static Point KeepOnScreen(Rect bounds, Rect screen)
    {
        var x = Math.Max(screen.Left, Math.Min(bounds.Left, screen.Right - bounds.Width));
        var y = Math.Max(screen.Top, Math.Min(bounds.Top, screen.Bottom - bounds.Height));

        return new Point(x, y);
    }

    /// <summary>
    /// Returns where the window actually is on screen, in the window's own units.
    /// This is read from the window itself because <see cref="Window.Left" /> and <see cref="Window.Top" /> can lag behind a position restored straight onto the window.
    /// </summary>
    public static Point GetTopLeft(Window window)
    {
        NativeMethods.GetWindowRect(new WindowInteropHelper(window).Handle, out var rect);
        return GetFromDevice(window).Transform(new Point(rect.Left, rect.Top));
    }

    /// <summary>
    /// Returns whether a point on the screen, in device pixels, is over the window.
    /// </summary>
    public static bool IsOverWindow(Window window, int x, int y)
    {
        if (!NativeMethods.GetWindowRect(new WindowInteropHelper(window).Handle, out var rect))
            return false;

        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
    }

    /// <summary>
    /// Returns the bounds of the monitor the window is on (or nearest to), in the window's own units.
    /// The whole monitor is used rather than the area above the taskbar, so a widget parked on the taskbar can stay there.
    /// </summary>
    public static Rect GetScreenBounds(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)) };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;

        var fromDevice = GetFromDevice(window);
        var topLeft = fromDevice.Transform(new Point(info.rcMonitor.Left, info.rcMonitor.Top));
        var bottomRight = fromDevice.Transform(new Point(info.rcMonitor.Right, info.rcMonitor.Bottom));

        return new Rect(topLeft, bottomRight);
    }

    private static Matrix GetFromDevice(Window window) =>
        PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

    private static class NativeMethods
    {
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    }
}
