using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Network_Monitor;

/// <summary>
/// Keeps the widget against the screen edges it's parked near when its size changes, and on its monitor.
/// Windows grow from their top-left corner, so a widget parked in a bottom or right corner would otherwise spill off the screen whenever it got bigger.
/// </summary>
/// <remarks>
/// Places on the screen are in device pixels, as Windows gives them, rather than in WPF's units.
/// Each monitor can have its own scale, and a window's <see cref="Window.Left" /> and <see cref="Window.Top" /> are in the units of whichever monitor it's on at the time, so they don't line up from one monitor to the next.
/// </remarks>
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
    /// Returns where a window at <paramref name="bounds" /> should go when its scale changes and it becomes <paramref name="newSize" />: against the same edges of <paramref name="screen" /> as before, and fully on it.
    /// </summary>
    public static Rect GetBoundsAfterScaleChange(Rect bounds, Size newSize, Rect screen) =>
        new(GetPositionAfterResize(bounds, newSize, screen, GetFixedEdges(bounds, screen)), newSize);

    /// <summary>
    /// Returns how big <paramref name="size" />, in WPF units, is in device pixels at the scale <paramref name="dpi" />.
    /// It's rounded to whole pixels, which is all a window can measure.
    /// </summary>
    public static Size ToDevicePixels(Size size, DpiScale dpi) =>
        new(Math.Round(size.Width * dpi.DpiScaleX), Math.Round(size.Height * dpi.DpiScaleY));

    /// <summary>
    /// Returns where the window actually is on screen, in device pixels.
    /// This is read from the window itself because <see cref="Window.Left" /> and <see cref="Window.Top" /> can lag behind a position restored straight onto the window.
    /// </summary>
    public static Rect GetBounds(Window window)
    {
        NativeMethods.GetWindowRect(new WindowInteropHelper(window).Handle, out var rect);
        return ToRect(rect);
    }

    /// <summary>
    /// Moves the window's top-left corner to <paramref name="position" />, in device pixels, without resizing or activating it.
    /// It's moved in one step, because setting <see cref="Window.Left" /> and then <see cref="Window.Top" /> could take it across another monitor on the way and change its scale partway.
    /// </summary>
    public static void MoveTo(Window window, Point position) =>
        NativeMethods.SetWindowPos(
            new WindowInteropHelper(window).Handle,
            IntPtr.Zero,
            (int)Math.Round(position.X),
            (int)Math.Round(position.Y),
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

    /// <summary>
    /// Changes the bounds Windows suggests for the window when its scale changes, so it stays against whichever edges of its monitor it's parked near, and fully on that monitor.
    /// Windows suggests the right size for the new scale but knows nothing of those edges, so a widget in a bottom or right corner could spill off the screen when its scale went up, such as when the monitor's scale is turned up in Settings.
    /// </summary>
    /// <param name="suggestedBounds">The lParam of WM_DPICHANGED: the suggested bounds in device pixels, which WPF moves and resizes the window to once the message reaches it.</param>
    public static void KeepEdgesForScaleChange(Window window, IntPtr suggestedBounds)
    {
        if (GetScreenBounds(window) is not Rect screen)
            return;

        var suggested = ToRect(Marshal.PtrToStructure<NativeMethods.RECT>(suggestedBounds));
        var bounds = GetBoundsAfterScaleChange(GetBounds(window), suggested.Size, screen);

        Marshal.StructureToPtr(ToNativeRect(bounds), suggestedBounds, false);
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
    /// Returns the bounds of the monitor the window is on (or nearest to), in device pixels, or null if Windows can't say, when the window is best left where it is.
    /// The whole monitor is used rather than the area above the taskbar, so a widget parked on the taskbar can stay there.
    /// </summary>
    public static Rect? GetScreenBounds(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)) };

        return NativeMethods.GetMonitorInfo(monitor, ref info) ? ToRect(info.rcMonitor) : null;
    }

    private static Rect ToRect(NativeMethods.RECT rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private static NativeMethods.RECT ToNativeRect(Rect rect) => new()
    {
        Left = (int)Math.Round(rect.Left),
        Top = (int)Math.Round(rect.Top),
        Right = (int)Math.Round(rect.Right),
        Bottom = (int)Math.Round(rect.Bottom),
    };

    private static class NativeMethods
    {
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

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

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
