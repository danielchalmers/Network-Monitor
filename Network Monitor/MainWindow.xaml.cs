using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private const int MK_CONTROL = 0x0008;

    private double _pendingWheelSteps;
    private bool _hasRendered;
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
        var bounds = new Rect(ScreenEdges.GetTopLeft(this), new Size(ActualWidth, ActualHeight));
        var position = ScreenEdges.KeepOnScreen(bounds, ScreenEdges.GetScreenBounds(this));

        Left = position.X;
        Top = position.Y;
        Activate();
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        _hasRendered = true;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Sizing while the window first appears isn't a resize, and moving it then would undo its restored position.
        if (!_hasRendered || e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0)
            return;

        var oldBounds = new Rect(ScreenEdges.GetTopLeft(this), e.PreviousSize);
        var screen = ScreenEdges.GetScreenBounds(this);
        _fixedEdges ??= ScreenEdges.GetFixedEdges(oldBounds, screen);
        var position = ScreenEdges.GetPositionAfterResize(oldBounds, e.NewSize, screen, _fixedEdges.Value);

        _isRepositioning = true;
        try
        {
            Left = position.X;
            Top = position.Y;
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

        try
        {
            WindowPlacementFunctions.SetPlacement(this, Settings.Default.Placement);
        }
        catch
        {
            // System.Configuration doesn't like the WindowPlacement struct sometimes.
        }
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
        SettingsSaver.SaveNow();
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
