using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;
using WpfWindowPlacement;

namespace Network_Monitor;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // DragMove throws if the left button isn't down anymore, which can happen with touch or pen input, so check it first.
        if (e.ChangedButton != MouseButton.Left || Mouse.LeftButton != MouseButtonState.Pressed)
            return;

        // Hold the displayed values while the window is grabbed so they don't change under the cursor.
        // DragMove blocks until the button is released, so the finally always resumes.
        ViewModel.UpdatesPaused = true;
        MonitorView.ToolTipsResumeAt = DateTime.MaxValue;
        MonitorView.CloseToolTip();
        try
        {
            DragMove();
        }
        finally
        {
            ViewModel.UpdatesPaused = false;

            // The cursor is still resting on the widget right after the drop, which isn't a request for the tooltip.
            MonitorView.ToolTipsResumeAt = DateTime.UtcNow.AddSeconds(1);
        }
    }

    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int WM_DPICHANGED = 0x02E0;
    private const int MK_CONTROL = 0x0008;

    private double _pendingWheelSteps;
    private Rect? _restoredBounds;
    private bool _hasRendered;
    private bool _isBeingMoved;
    private ScreenEdges.FixedEdges? _fixedEdges;
    private bool _isRepositioning;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Resize with Ctrl+scroll. Ctrl is read from the wheel message itself rather than the keyboard state, which Windows doesn't promise to keep current for a window that isn't active, and the widget usually isn't.
        if (msg == WM_MOUSEWHEEL)
        {
            // A 64-bit wParam or lParam can't be cast straight to int, so take the parts from the full value.
            var data = wParam.ToInt64();
            var point = lParam.ToInt64();

            // Windows can send the wheel to the active window wherever the pointer is, so only resize when it's over the widget.
            if ((data & MK_CONTROL) != 0 && ScreenEdges.IsOverWindow(this, (short)(point & 0xFFFF), (short)((point >> 16) & 0xFFFF)))
            {
                ResizeByWheel((short)((data >> 16) & 0xFFFF));
                handled = true;
            }
        }

        // While the window is dragged, or moved with the keyboard, Windows moves it along with the pointer, and nothing else should move it.
        if (msg == WM_ENTERSIZEMOVE)
            _isBeingMoved = true;
        else if (msg == WM_EXITSIZEMOVE)
            _isBeingMoved = false;

        // The window's scale changes when its monitor's does, or when it's moved onto a monitor with a different one, and WPF then resizes it to the bounds Windows suggests, which it reads after this.
        // So the suggestion is changed first to keep the widget against its edges, except while it's dragged, when it should stay under the pointer.
        // Nor while it first appears, when its size isn't yet one WPF laid out at the old scale, such as the size it was saved at, and WPF sizes it to fit its readings anyway. It's put against its edges once it's appeared instead.
        if (msg == WM_DPICHANGED && _hasRendered && !_isBeingMoved && WindowState == WindowState.Normal)
            ScreenEdges.KeepEdgesForScaleChange(this, lParam);

        return IntPtr.Zero;
    }

    private void ResizeByWheel(int delta)
    {
        // Precision touchpads send many small steps, which are added up so they still resize the widget rather than each rounding away to nothing.
        _pendingWheelSteps += delta / (double)Mouse.MouseWheelDeltaForOneLine;

        var size = Settings.Default.Size;
        var newSize = SizeScaleConverter.ScaleSize(size, _pendingWheelSteps);

        if (newSize != size)
        {
            Settings.Default.Size = newSize;
            _pendingWheelSteps = 0;
        }
        else if ((_pendingWheelSteps > 0 && size >= SizeScaleConverter.MaxSize) || (_pendingWheelSteps < 0 && size <= SizeScaleConverter.MinSize))
        {
            // Scrolling further past the limit shouldn't have to be undone before scrolling back.
            _pendingWheelSteps = 0;
        }
    }

    /// <summary>
    /// Brings the widget into view when its exe is started again: in front of other windows, and fully back on a screen if it was partly off one.
    /// </summary>
    public void Reveal()
    {
        if (ScreenEdges.GetScreenBounds(this) is Rect screen)
            ScreenEdges.MoveTo(this, ScreenEdges.KeepOnScreen(ScreenEdges.GetBounds(this), screen));

        Activate();
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        _hasRendered = true;

        // The widget is laid out at its monitor's scale now, so it isn't the size it was saved at if that monitor's scaling has changed since, or if 4.x saved it on a monitor scaled differently from the main one.
        // It changed size from its top-left corner, which could leave it past the monitor's edge, so it's put back against the edges it was parked near, and fully on the monitor, as when it's resized.
        var size = ScreenEdges.GetBounds(this).Size;

        if (_restoredBounds is Rect restored && size != restored.Size && ScreenEdges.GetScreenBounds(this) is Rect screen)
            ScreenEdges.MoveTo(this, ScreenEdges.GetBoundsAfterScaleChange(restored, size, screen).TopLeft);

        // Say so, as DesktopClock does, rather than quietly losing every change when the widget closes.
        // It's usually a folder that needs admin rights, but the file can also be read-only, or kept busy by another app the whole time the widget was starting.
        if (!Settings.Default.CanBeSaved)
        {
            MessageBox.Show(this,
                "Network Monitor can't save its settings right now, so changes won't be kept.\n\nIf this keeps happening, move it to a folder that doesn't need admin rights.",
                "Network Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Sizing while the window first appears isn't a resize, and moving it then would undo its restored position.
        // Nor is the small change in size when it's dragged onto a monitor with a different scale, when it should stay under the pointer.
        if (!_hasRendered || _isBeingMoved || e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0)
            return;

        if (ScreenEdges.GetScreenBounds(this) is not Rect screen)
            return;

        // The sizes are in WPF units, so they're measured in device pixels like the screen, at the window's scale now.
        var dpi = VisualTreeHelper.GetDpi(this);
        var oldBounds = new Rect(ScreenEdges.GetBounds(this).TopLeft, ScreenEdges.ToDevicePixels(e.PreviousSize, dpi));
        _fixedEdges ??= ScreenEdges.GetFixedEdges(oldBounds, screen);
        var position = ScreenEdges.GetPositionAfterResize(oldBounds, ScreenEdges.ToDevicePixels(e.NewSize, dpi), screen, _fixedEdges.Value);

        _isRepositioning = true;
        try
        {
            ScreenEdges.MoveTo(this, position);
        }
        finally
        {
            _isRepositioning = false;
        }
    }

    private void MenuItemAdapter_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        var adapters = NetworkAdapters.GetMonitorable()
            .OrderBy(x => x.Name)
            .ToList();

        var internetAdapter = NetworkAdapters.FindInternetAdapter(adapters);

        AdapterMenuItem.Items.Clear();
        AdapterMenuItem.Items.Add(CreateAdapterMenuItem(internetAdapter is null ? "_Automatic" : $"_Automatic (now {EscapeAccessKeys(internetAdapter.Name)})", string.Empty));
        AdapterMenuItem.Items.Add(new Separator());

        foreach (var adapter in adapters)
            AdapterMenuItem.Items.Add(CreateAdapterMenuItem(EscapeAccessKeys(adapter.Name), adapter.Id));

        // Keep a saved adapter selectable while it's unplugged so the choice is visible and can be changed.
        var selectedId = Settings.Default.InterfaceId;

        if (!string.IsNullOrEmpty(selectedId) && selectedId != NetworkAdapters.AllAdapters && !adapters.Any(x => x.Id == selectedId))
            AdapterMenuItem.Items.Add(CreateAdapterMenuItem("(Disconnected)", selectedId));

        AdapterMenuItem.Items.Add(new Separator());
        AdapterMenuItem.Items.Add(CreateAdapterMenuItem("A_ll adapters", NetworkAdapters.AllAdapters));
    }

    /// <summary>
    /// Doubles underscores so adapter names don't turn into access keys.
    /// </summary>
    private static string EscapeAccessKeys(string text) => text.Replace("_", "__");

    private static MenuItem CreateAdapterMenuItem(string header, string interfaceId)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = Settings.Default.InterfaceId == interfaceId,
        };

        item.Click += (_, _) => Settings.Default.InterfaceId = interfaceId;

        return item;
    }

    private void MenuItemPingAddress_OnClick(object sender, RoutedEventArgs e)
    {
        // Owned by the widget, so it stays in front of it when the widget is always on top.
        var dialog = new PingAddressDialog(Settings.Default.PingHost) { Owner = this };

        // The latency reading moves to it on the next tick.
        if (dialog.ShowDialog() == true)
            Settings.Default.PingHost = dialog.Address;
    }

    private void MenuItemCopy_OnClick(object sender, RoutedEventArgs e)
    {
        CopyOverview();
    }

    private void ContextMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        try
        {
            StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled(App.ExePath);
        }
        catch
        {
            StartWithWindowsMenuItem.IsChecked = false;
        }
    }

    private void MenuItemStartWithWindows_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (StartupRegistration.IsEnabled(App.ExePath))
                StartupRegistration.Disable();
            else
                StartupRegistration.Enable(App.ExePath);
        }
        catch
        {
            // A policy can make the Run key read-only.
            SystemSounds.Beep.Play();
        }
    }

    private void MenuItemTheme_OnClick(object sender, RoutedEventArgs e)
    {
        Settings.Default.Theme = (AppTheme)((MenuItem)sender).Tag;
    }

    private void MenuItemCheckForUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/danielchalmers/Network-Monitor/releases/latest");
    }

    private void MenuItemGiveFeedback_OnClick(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/danielchalmers/Network-Monitor/issues");
    }

    private void MenuItemExit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);

        var placement = Settings.Default.Placement;
        WindowPlacementFunctions.SetPlacement(this, placement);

        // Where it was put back, at the size it was saved at, so once it's laid out it can tell whether that changed. A placement with no size, like the first time, has nothing to compare.
        // The position is read from the window because a placement's is measured from the work area, which starts beside the taskbar when that's at the top or left.
        var saved = placement.NormalBounds;

        if (saved.Right > saved.Left && saved.Bottom > saved.Top)
            _restoredBounds = new Rect(ScreenEdges.GetBounds(this).TopLeft, new Size(saved.Right - saved.Left, saved.Bottom - saved.Top));
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        // Once the widget has been moved somewhere else, the next resize decides afresh which edges to keep fixed.
        if (!_isRepositioning)
            _fixedEdges = null;

        // Remember the position as it changes, so it survives a crash or a forced restart and not only a clean exit.
        // Moves before the window has loaded are just the saved position being restored.
        if (IsLoaded)
            Settings.Default.Placement = WindowPlacementFunctions.GetPlacement(this);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        Settings.Default.Placement = WindowPlacementFunctions.GetPlacement(this);

        if (Settings.Default.CanBeSaved)
            Settings.Default.Save();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            CopyOverview();
    }

    /// <summary>
    /// Copies every monitor's stats to the clipboard.
    /// Another app can hold the clipboard open for a moment, such as a clipboard manager or a remote desktop session, so a failure beeps instead of closing the widget.
    /// </summary>
    private void CopyOverview()
    {
        try
        {
            Clipboard.SetText(ViewModel.GetOverviewText());
        }
        catch
        {
            SystemSounds.Beep.Play();
        }
    }

    /// <summary>
    /// Opens a web page in the default browser, beeping instead of crashing if there's no browser to open it.
    /// </summary>
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            SystemSounds.Beep.Play();
        }
    }
}
