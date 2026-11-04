using System;
using System.Runtime.InteropServices;
using System.Windows;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Hides the widget while a fullscreen app, like a game, a video, or a slideshow, is in front on the monitor it's on, and shows it again when that ends, as DesktopClock does.
/// </summary>
/// <remarks>
/// It checks on the readings' one-second tick rather than on a timer of its own, so a fullscreen app is caught within a second.
/// The setting is read on each tick too, so turning it off brings the widget back by the next one.
/// </remarks>
public sealed class FullscreenHideManager : IDisposable
{
    private readonly Window _window;
    private bool _isDisposed;

    /// <summary>
    /// The fullscreen window the widget is hidden for, or <see cref="IntPtr.Zero" /> while it's showing.
    /// </summary>
    private IntPtr _hiddenFor;

    /// <summary>
    /// Hides <paramref name="window" /> for fullscreen apps while <see cref="Settings.HideWhenFullscreen" /> is on.
    /// </summary>
    public FullscreenHideManager(Window window)
    {
        _window = window;
        Monitor.ClockTicked += Clock_Ticked;
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
        _isDisposed = true;
        Monitor.ClockTicked -= Clock_Ticked;
    }

    /// <summary>
    /// Checks on the window's thread, since the tick comes on a thread pool thread, which can't hide or show the window.
    /// </summary>
    private void Clock_Ticked(object sender, EventArgs e) => _window.Dispatcher.BeginInvoke(new Action(Update));

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
        var screen = ScreenEdges.GetScreenBounds(_window);
        var front = NativeMethods.GetForegroundWindow();

        if (IsFullscreen(front, screen))
            return front;

        // Any other app's window in front on the widget's monitor brings it back.
        if (IsApp(front) && ScreenEdges.GetScreenBounds(front) == screen)
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
        FillsMonitor(ScreenEdges.GetBounds(window), screen);

    /// <summary>
    /// Returns whether <paramref name="window" /> is an app's window that can be seen.
    /// That leaves out the shell's windows, like the desktop that fills the monitor after Win+D, the taskbar, and Alt+Tab.
    /// </summary>
    /// <remarks>
    /// The shell's windows are told apart by their process rather than their names, which have changed from one version of Windows to the next.
    /// </remarks>
    private static bool IsApp(IntPtr window)
    {
        if (!NativeMethods.IsWindowVisible(window))
            return false;

        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        NativeMethods.GetWindowThreadProcessId(NativeMethods.GetShellWindow(), out var shellProcessId);

        return processId != shellProcessId;
    }

    private void Hide(IntPtr fullscreenWindow)
    {
        if (_hiddenFor == IntPtr.Zero)
        {
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

        // It comes back where it was without taking the focus, as ShowActivated is off, so whatever's in front, like the app that just left fullscreen, keeps the keyboard.
        _window.Show();
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hwnd);
    }
}
