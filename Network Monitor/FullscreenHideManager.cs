using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Hides the widget while a fullscreen app, like a game, a video, or a slideshow, fills the monitor it's on, and shows it again when that ends, as DesktopClock does.
/// </summary>
/// <remarks>
/// It checks as soon as another window comes to the front, from an event Windows sends, so the widget is out of the way the moment a fullscreen game is switched to.
/// A window that goes fullscreen while it's already in front, like a video in a browser, is caught on the readings' next tick, within a second, rather than on a timer of its own that would wake the widget at other moments.
/// So is one that fills the widget's monitor without having the focus, like a slideshow on a projector while Presenter View has the focus on the laptop's screen.
/// </remarks>
public sealed class FullscreenHideManager : IDisposable
{
    /// <summary>
    /// How a window, like the one in front, stands with the widget's monitor.
    /// </summary>
    public enum FrontWindow
    {
        /// <summary>
        /// No window, or one that says nothing about whether the widget should hide: one of its own, or one of the shell's, like the desktop that Win+D shows, the taskbar, or Alt+Tab.
        /// </summary>
        Ignored,

        /// <summary>
        /// An app's window on another monitor.
        /// It says no more than an ignored one about whether the widget should hide, and is only told apart from one to make clear why.
        /// </summary>
        Elsewhere,

        /// <summary>
        /// An app's window on the widget's monitor that doesn't fill it.
        /// </summary>
        Windowed,

        /// <summary>
        /// An app's window that fills the widget's monitor.
        /// </summary>
        Fullscreen,
    }

    /// <summary>
    /// How the window the widget is hidden for stands now.
    /// </summary>
    public enum HiddenForWindow
    {
        /// <summary>
        /// None, or one that's been closed, minimized, or taken out of fullscreen.
        /// </summary>
        Gone,

        /// <summary>
        /// One that still fills the widget's monitor.
        /// </summary>
        StillFills,

        /// <summary>
        /// One on a virtual desktop that's been switched away from, which is the widget's desktop too, since it's only hidden for windows on its own.
        /// </summary>
        OnAnotherDesktop,
    }

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
    /// The fullscreen window the widget was last brought back in front of when it was asked to come into view, which only hides it again by coming to the front itself, not by being on top on its monitor.
    /// </summary>
    private IntPtr _shownOver;

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
    /// Returns whether a window at <paramref name="bounds" /> fills <paramref name="monitor" /> as a fullscreen game, video, or slideshow does, reaching every edge of it or past them, both in device pixels.
    /// A maximized window with a title bar doesn't count, though on a monitor without a taskbar, or with one that hides itself, its borders reach past every edge too.
    /// </summary>
    public static bool FillsMonitor(Rect bounds, Rect monitor, bool isMaximizedWithTitleBar) =>
        !isMaximizedWithTitleBar &&
        bounds.Left <= monitor.Left &&
        bounds.Top <= monitor.Top &&
        bounds.Right >= monitor.Right &&
        bounds.Bottom >= monitor.Bottom;

    /// <summary>
    /// Returns how a window stands with the widget's monitor.
    /// </summary>
    /// <param name="isShowing">Whether the window can be seen: it's shown, and isn't minimized or cloaked.</param>
    /// <param name="isOwnOrShell">Whether it's one of the widget's own windows or one of the shell's.</param>
    /// <param name="fillsMonitor">Whether it fills the widget's monitor, as <see cref="FillsMonitor(Rect, Rect, bool)" /> describes.</param>
    /// <param name="isOnMonitor">Whether it's on the widget's monitor, or mostly on it if it's across several.</param>
    public static FrontWindow Classify(bool isShowing, bool isOwnOrShell, bool fillsMonitor, bool isOnMonitor)
    {
        // A window that can't be seen, like one that's just been minimized, says nothing about what's on the screen, and the desktop fills the monitor without being fullscreen.
        if (!isShowing || isOwnOrShell)
            return FrontWindow.Ignored;

        // A window across several monitors, like a game spread over all of them, can fill the widget's without being counted as on it.
        if (fillsMonitor)
            return FrontWindow.Fullscreen;

        return isOnMonitor ? FrontWindow.Windowed : FrontWindow.Elsewhere;
    }

    /// <summary>
    /// Returns the window the widget should be hidden for, or <see cref="IntPtr.Zero" /> if it should be showing.
    /// A fullscreen window in front on its monitor hides it, and any other app's window in front there brings it back.
    /// While the window in front says nothing about its monitor, like one on another monitor, or the desktop, it stays hidden for as long as the window it's hidden for still fills its monitor, so a video left fullscreen beside the window being worked in isn't covered.
    /// Otherwise it's hidden for a window that fills its monitor and is on top there, though another window has the focus.
    /// While the window it's hidden for is on a virtual desktop that's been switched away from, it stays hidden whatever's in front, because showing it would put it on the desktop in use rather than its own.
    /// </summary>
    /// <param name="front">The window in front.</param>
    /// <param name="kind">How <paramref name="front" /> stands with the widget's monitor.</param>
    /// <param name="hiddenFor">The window the widget is hidden for now, or <see cref="IntPtr.Zero" /> if it's showing.</param>
    /// <param name="hiddenForNow">How <paramref name="hiddenFor" /> stands now.</param>
    /// <param name="fullscreenOnTop">A window that fills the widget's monitor and is on top there without being in front, like a slideshow on a projector while Presenter View has the focus on the laptop's screen, or <see cref="IntPtr.Zero" /> if there's none.</param>
    public static IntPtr GetWindowToHideFor(IntPtr front, FrontWindow kind, IntPtr hiddenFor, HiddenForWindow hiddenForNow, IntPtr fullscreenOnTop)
    {
        if (hiddenForNow == HiddenForWindow.OnAnotherDesktop)
            return hiddenFor;

        return kind switch
        {
            FrontWindow.Fullscreen => front,
            FrontWindow.Windowed => IntPtr.Zero,
            _ => hiddenForNow == HiddenForWindow.StillFills ? hiddenFor : fullscreenOnTop,
        };
    }

    /// <summary>
    /// Shows the widget if it's hidden, such as when it's asked to come into view.
    /// It's hidden again once a fullscreen app is next found in front on its monitor, but not by the one it was hidden for just being on top there.
    /// </summary>
    public void ShowNow()
    {
        // It can be asked just after the window has closed, such as while Windows signs out, and a closed window can't be shown again.
        if (_isDisposed)
            return;

        if (_hiddenFor != IntPtr.Zero)
            _shownOver = _hiddenFor;

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
        // Windows calls a hook back on the thread that set it, which also has to be the one that removes it, so that's always the window's thread, whichever thread changed the setting.
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

        // Checked once the window has finished what it's doing, which the first time is showing itself. Turned off, this brings the widget back.
        QueueUpdate();
    }

    private void StartWatching()
    {
        if (_isWatching)
            return;

        _isWatching = true;

        // Out of context, Windows calls back on this thread between the messages it handles, rather than from inside the other app.
        // The widget's own windows coming to the front are left out, since they never hide or show it.
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

        return GetWindowToHideFor(
            front,
            Describe(front, monitor, screen),
            _hiddenFor,
            DescribeHiddenFor(screen),
            FindFullscreenOnTop(monitor, screen));
    }

    /// <summary>
    /// Returns how <paramref name="window" /> stands with the widget's <paramref name="monitor" />, whose bounds are <paramref name="screen" />.
    /// </summary>
    private static FrontWindow Describe(IntPtr window, IntPtr monitor, Rect screen) =>
        Classify(
            IsShowing(window),
            IsOwnOrShell(window),
            FillsMonitor(window, screen),
            NativeMethods.MonitorFromWindow(window, NativeMethods.MONITOR_DEFAULTTONEAREST) == monitor);

    /// <summary>
    /// Returns how the window the widget is hidden for stands now, against the bounds of the widget's monitor, <paramref name="screen" />.
    /// </summary>
    private HiddenForWindow DescribeHiddenFor(Rect screen)
    {
        if (_hiddenFor == IntPtr.Zero)
            return HiddenForWindow.Gone;

        if (IsShowing(_hiddenFor))
            return FillsMonitor(_hiddenFor, screen) ? HiddenForWindow.StillFills : HiddenForWindow.Gone;

        return IsOnAnotherDesktop(_hiddenFor) ? HiddenForWindow.OnAnotherDesktop : HiddenForWindow.Gone;
    }

    /// <summary>
    /// Returns the window on top on the widget's <paramref name="monitor" />, whose bounds are <paramref name="screen" />, if it fills it and isn't the one the widget was brought back in front of, or <see cref="IntPtr.Zero" /> if not.
    /// </summary>
    private IntPtr FindFullscreenOnTop(IntPtr monitor, Rect screen)
    {
        // It's looked for halfway down the monitor, a quarter of the way across from the side further from the widget, so the widget itself isn't what's found.
        // Windows that let clicks through are passed over there, as they are by the mouse.
        NativeMethods.GetWindowRect(_handle, out var widget);
        var isWidgetOnLeft = (widget.Left + widget.Right) / 2.0 < screen.Left + (screen.Width / 2);
        var point = new NativeMethods.POINT
        {
            X = (int)(isWidgetOnLeft ? screen.Right - (screen.Width / 4) : screen.Left + (screen.Width / 4)),
            Y = (int)(screen.Top + (screen.Height / 2)),
        };

        var window = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point), NativeMethods.GA_ROOT);

        // The widget was brought back in front of that one on purpose, so it's left to hide it by coming to the front.
        return window != _shownOver && Describe(window, monitor, screen) == FrontWindow.Fullscreen ? window : IntPtr.Zero;
    }

    /// <summary>
    /// Returns whether <paramref name="window" /> is the widget's own, like its menu or the Ping address box, or the shell's: the desktop, which fills the monitor whenever nothing else is in front, such as after Win+D, and the taskbar, Alt+Tab, and Task View.
    /// </summary>
    /// <remarks>
    /// The shell's windows are told apart by their process rather than their names, which have changed from one version of Windows to the next.
    /// File Explorer's windows usually share that process, so they count as the shell's too.
    /// Start, Search, and the notification center have processes of their own, so they count as apps' windows that don't fill the monitor, and opening one over a fullscreen game shows the widget until the game's back in front, as it does the taskbar.
    /// </remarks>
    private static bool IsOwnOrShell(IntPtr window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);

        if (processId == NativeMethods.GetCurrentProcessId())
            return true;

        NativeMethods.GetWindowThreadProcessId(NativeMethods.GetShellWindow(), out var shellProcessId);

        return shellProcessId != 0 && processId == shellProcessId;
    }

    /// <summary>
    /// Returns whether <paramref name="window" /> can be seen: it's shown, isn't minimized, and isn't cloaked, as a window on another virtual desktop is.
    /// </summary>
    private static bool IsShowing(IntPtr window) =>
        NativeMethods.IsWindowVisible(window) &&
        !NativeMethods.IsIconic(window) &&
        GetCloaked(window) == 0;

    /// <summary>
    /// Returns whether <paramref name="window" /> is on a virtual desktop other than the one in use: it's shown, and isn't minimized, but the shell has cloaked it.
    /// </summary>
    private static bool IsOnAnotherDesktop(IntPtr window) =>
        NativeMethods.IsWindowVisible(window) &&
        !NativeMethods.IsIconic(window) &&
        (GetCloaked(window) & NativeMethods.DWM_CLOAKED_SHELL) != 0;

    /// <summary>
    /// Returns why <paramref name="window" /> is cloaked, as flags saying who cloaked it, or 0 if it isn't.
    /// </summary>
    private static int GetCloaked(IntPtr window) =>
        NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 ? cloaked : 0;

    /// <summary>
    /// Returns whether <paramref name="window" /> fills the monitor whose bounds are <paramref name="screen" />, as <see cref="FillsMonitor(Rect, Rect, bool)" /> describes.
    /// </summary>
    private static bool FillsMonitor(IntPtr window, Rect screen) =>
        NativeMethods.GetWindowRect(window, out var rect) &&
        FillsMonitor(ToRect(rect), screen, IsMaximizedWithTitleBar(window));

    private static bool IsMaximizedWithTitleBar(IntPtr window) =>
        NativeMethods.IsZoomed(window) &&
        (NativeMethods.GetWindowLong(window, NativeMethods.GWL_STYLE) & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION;

    private void Hide(IntPtr fullscreenWindow)
    {
        if (_hiddenFor == IntPtr.Zero)
        {
            // A minimized widget is out of the way already, and hiding it would take away its taskbar button. One that isn't showing at all wasn't hidden by this, so it isn't this one's to bring back.
            // Nor is one on a virtual desktop that isn't in use, which is out of the way too, and which showing again could move to the desktop that is.
            if (!IsShowing(_handle))
                return;

            // A reading's tooltip is a window of its own, which could otherwise stay open over the fullscreen app for up to a minute.
            MonitorView.CloseToolTip();
            _window.Hide();

            // Once it's been hidden again, the window it was brought back in front of is like any other.
            _shownOver = IntPtr.Zero;
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
        public const int GWL_STYLE = -16;
        public const int WS_CAPTION = 0x00C00000;
        public const int DWMWA_CLOAKED = 14;
        public const int DWM_CLOAKED_SHELL = 0x2;
        public const uint GA_ROOT = 2;

        public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint eventThread, uint eventTime);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
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
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

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
