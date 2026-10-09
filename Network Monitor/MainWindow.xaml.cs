using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
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

        Settings.Default.PropertyChanged += Settings_PropertyChanged;

        DataContext = new MainViewModel();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.Default.RunOnStartup):

                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (Settings.Default.RunOnStartup)
                        key?.SetValue("Network_Monitor", App.ResourceAssembly.Location);
                    else
                        key?.DeleteValue("Network_Monitor", false);
                }

                break;
        }
    }

    private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // DragMove throws if the left button isn't down anymore, which can happen with touch or pen input, so check it first.
        if (e.ChangedButton != MouseButton.Left || Mouse.LeftButton != MouseButtonState.Pressed)
            return;

        // Hold the displayed values while the window is grabbed so they don't change under the cursor.
        // DragMove blocks until the button is released, so the finally always resumes.
        ViewModel.UpdatesPaused = true;
        try
        {
            DragMove();
        }
        finally
        {
            ViewModel.UpdatesPaused = false;
        }
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Scale size based on scroll amount, with one notch on a default PC mouse being a change of 15%.
            var steps = e.Delta / (double)Mouse.MouseWheelDeltaForOneLine;
            var change = Settings.Default.Size * steps * 0.15;
            Settings.Default.Size = (int)Math.Min(Math.Max(Settings.Default.Size + change, 32), 320);
        }
    }

    private void MenuItemAdapter_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        AdapterMenuItem.Items.Clear();
        AdapterMenuItem.Items.Add(CreateAdapterMenuItem("_All", string.Empty));

        var adapters = NetworkAdapters.GetMonitorable()
            .OrderBy(x => x.Name)
            .ToList();

        foreach (var adapter in adapters)
        {
            // Doubled underscores so adapter names don't turn into access keys.
            AdapterMenuItem.Items.Add(CreateAdapterMenuItem(adapter.Name.Replace("_", "__"), adapter.Id));
        }

        // Keep a saved adapter selectable while it's unplugged so the choice is visible and can be changed.
        var selectedId = Settings.Default.InterfaceId;

        if (!string.IsNullOrEmpty(selectedId) && !adapters.Any(x => x.Id == selectedId))
            AdapterMenuItem.Items.Add(CreateAdapterMenuItem("(Disconnected)", selectedId));
    }

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
