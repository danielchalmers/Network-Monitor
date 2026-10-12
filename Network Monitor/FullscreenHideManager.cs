using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Hides the widget while a fullscreen app, like a game, a video, or a slideshow, is in front on the monitor it's on, and shows it again when that ends, as DesktopClock does.
/// </summary>
/// <remarks>
/// It checks as soon as another window comes to the front, from an event Windows sends, so the widget is out of the way the moment a fullscreen game is switched to.
/// A window that goes fullscreen while it's already in front, like a video in a browser, is caught on the readings' next tick, within a second, rather than on a timer of its own.
/// </remarks>
public sealed class FullscreenHideManager : IDisposable
{
    private readonly Window _window;
    private readonly IntPtr _handle;

    /// <summary>
    /// Called by Windows when a different window comes to the front.
    /// It's kept in a field because Windows only holds a pointer to it, which doesn't stop it from being garbage collected.
    /// </summary>
    private readonly NativeMethods.WinEventProc _onForegroundChanged;

    private IntPtr _foregroundHook;
    private bool _isWatching;
    private bool _isDisposed;

    /// <summary>
    /// The fullscreen window the widget is hidden for, or <see cref="IntPtr.Zero" /> while it's showing.
    /// </summary>
    private IntPtr _hiddenFor;

    /// <summary>
    /// Hides <paramref name="window" /> for fullscreen apps while <see cref="Settings.HideWhenFullscreen" /> is on.
    /// Its handle has to exist already, as it does from <see cref="Window.SourceInitialized" />.
    /// </summary>
    public FullscreenHideManager(Window window)
    {
        _window = window;
        _handle = new WindowInteropHelper(window).Handle;
        _onForegroundChanged = (_, _, _, _, _, _, _) => QueueUpdate();

        Settings.Default.PropertyChanged += Settings_PropertyChanged;
        ApplySetting();
    }

    /// <summary>
    /// Returns whether a window at <paramref name="bounds" /> fills <paramref name="monitor" />, reaching every edge of it or past them, both in device pixels.
    /// </summary>
    public static bool FillsMonitor(Rect bounds, Rect monitor) =>
        bounds.Left <= monitor.Left &&
        bounds.Top <= monitor.Top &&
        bounds.Right >= monitor.Right &&
        bounds.Bottom >= monitor.Bottom;

    /// <summary>
    /// Shows the widget if it's hidden, such as when it's asked to come into view.
    /// It stays in front of the fullscreen app until that's brought to the front again.
    /// </summary>
    public void ShowNow()
    {
        // It can be asked just after the window has closed, such as while Windows signs out, and a closed window can't be shown again.
        if (!_isDisposed)
            Show();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopWatching();
        Settings.Default.PropertyChanged -= Settings_PropertyChanged;
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // Windows calls a hook back on the thread that set it, which also has to be the one that removes it, so that's always the window's thread.
        if (e.PropertyName == nameof(Settings.HideWhenFullscreen))
            _window.Dispatcher.BeginInvoke(new Action(ApplySetting));
    }

    /// <summary>
    /// Watches for fullscreen apps while the setting is on, and only then, so it costs nothing while it's off.
    /// </summary>
    private void ApplySetting()
    {
        if (_isDisposed)
            return;

        if (Settings.Default.HideWhenFullscreen)
            StartWatching();
        else
            StopWatching();

        // Turned off, this brings the widget back.
        QueueUpdate();
    }

    private void StartWatching()
    {
        if (_isWatching)
            return;

        _isWatching = true;

        // Out of context, Windows calls back on this thread between the messages it handles, rather than from inside the other app.
        // If the hook can't be set, the tick still catches a fullscreen app within a second.
        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _onForegroundChanged,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        Monitor.ClockTicked += Clock_Ticked;
    }

    private void StopWatching()
    {
        if (!_isWatching)
            return;

        _isWatching = false;

        if (_foregroundHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        Monitor.ClockTicked -= Clock_Ticked;
    }

    private void Clock_Ticked(object sender, EventArgs e) => QueueUpdate();

    /// <summary>
    /// Checks on the window's thread, since the tick comes on a thread pool thread, which can't hide or show the window.
    /// </summary>
    private void QueueUpdate() => _window.Dispatcher.BeginInvoke(new Action(Update));

    private void Update()
    {
        if (_isDisposed)
            return;

        var hiddenFor = Settings.Default.HideWhenFullscreen ? FindWindowToHideFor() : IntPtr.Zero;

        if (hiddenFor == IntPtr.Zero)
            Show();
        else
            Hide(hiddenFor);
    }

    /// <summary>
    /// Returns the fullscreen window the widget should be hidden for, or <see cref="IntPtr.Zero" /> if it should be showing.
    /// </summary>
    private IntPtr FindWindowToHideFor()
    {
        var monitor = NativeMethods.MonitorFromWindow(_handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)) };

        // With nothing to measure windows against, the widget is better left showing.
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return IntPtr.Zero;

        var screen = ToRect(info.rcMonitor);
        var front = NativeMethods.GetForegroundWindow();

        if (IsFullscreen(front, screen))
            return front;

        // Any other app's window in front on the widget's monitor brings it back.
        if (IsApp(front) && NativeMethods.MonitorFromWindow(front, NativeMethods.MONITOR_DEFAULTTONEAREST) == monitor)
            return IntPtr.Zero;

        // Otherwise, like with the desktop or a window on another monitor in front, it stays hidden while the window it's hidden for still fills its monitor, so a video left fullscreen beside the window being worked in isn't covered.
        return _hiddenFor != IntPtr.Zero && IsFullscreen(_hiddenFor, screen) ? _hiddenFor : IntPtr.Zero;
    }

    /// <summary>
    /// Returns whether <paramref name="window" /> is an app's window that fills the monitor whose bounds are <paramref name="screen" />.
    /// A maximized window doesn't count, though on a monitor without a taskbar, or with one that hides itself, its borders reach past every edge too.
    /// </summary>
    private static bool IsFullscreen(IntPtr window, Rect screen) =>
        IsApp(window) &&
        !NativeMethods.IsZoomed(window) &&
        NativeMethods.GetWindowRect(window, out var rect) &&
        FillsMonitor(ToRect(rect), screen);

    /// <summary>
    /// Returns whether <paramref name="window" /> is an app's window that can be seen and clicked.
    /// That leaves out the widget's own windows, the shell's, like the desktop that fills the monitor after Win+D, the taskbar, and Alt+Tab, and overlays that let clicks through.
    /// </summary>
    /// <remarks>
    /// The shell's windows are told apart by their process rather than their names, which have changed from one version of Windows to the next.
    /// </remarks>
    private static bool IsApp(IntPtr window)
    {
        var exStyle = NativeMethods.GetWindowLong(window, NativeMethods.GWL_EXSTYLE);

        if (!IsShowing(window) || (exStyle & NativeMethods.WS_EX_CLICKTHROUGH) == NativeMethods.WS_EX_CLICKTHROUGH)
            return false;

        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        NativeMethods.GetWindowThreadProcessId(NativeMethods.GetShellWindow(), out var shellProcessId);

        return processId != NativeMethods.GetCurrentProcessId() && processId != shellProcessId;
    }

    /// <summary>
    /// Returns whether <paramref name="window" /> can be seen: it's shown, isn't minimized, and isn't cloaked, as a window on another virtual desktop is.
    /// </summary>
    private static bool IsShowing(IntPtr window) =>
        NativeMethods.IsWindowVisible(window) &&
        !NativeMethods.IsIconic(window) &&
        (NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int)) != 0 || cloaked == 0);

    private void Hide(IntPtr fullscreenWindow)
    {
        if (_hiddenFor == IntPtr.Zero)
        {
            // A minimized widget is out of the way already, and hiding it would take away its taskbar button.
            if (!IsShowing(_handle))
                return;

            // A reading's tooltip is a window of its own, which could otherwise stay open over the fullscreen app for up to a minute.
            MonitorView.CloseToolTip();
            _window.Hide();
        }

        // It can go straight from one fullscreen app to another, like from a video to a game, and stays hidden for whichever is in front.
        _hiddenFor = fullscreenWindow;
    }

    private void Show()
    {
        if (_hiddenFor == IntPtr.Zero)
            return;

        _hiddenFor = IntPtr.Zero;

        // It comes back where it was without taking the focus, so whatever's in front, like the app that just left fullscreen, keeps the keyboard.
        var showActivated = _window.ShowActivated;
        _window.ShowActivated = false;
        _window.Show();
        _window.ShowActivated = showActivated;
    }

    /// <summary>
    /// Returns a rectangle from Windows as a <see cref="Rect" />. Another app's window could report a negative size, which <see cref="Rect" /> would throw on, so it's taken as empty.
    /// </summary>
    private static Rect ToRect(NativeMethods.RECT rect) =>
        new(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));

    private static class NativeMethods
    {
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const int GWL_EXSTYLE = -20;

        /// <summary>
        /// WS_EX_LAYERED and WS_EX_TRANSPARENT together, as overlays that clicks pass through have, like a game's or a screen recorder's.
        /// </summary>
        public const int WS_EX_CLICKTHROUGH = 0x00080000 | 0x00000020;

        public const int DWMWA_CLOAKED = 14;

        public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint eventThread, uint eventTime);

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
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();
    }
}
