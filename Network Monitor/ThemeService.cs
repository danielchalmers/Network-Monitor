using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Single source of truth for the effective theme.
/// Light and Dark force the appearance; Auto follows the operating system's light/dark setting and accent color, updating live when either changes.
/// </summary>
public sealed class ThemeService : ObservableObject
{
    public static ThemeService Instance { get; } = new();

    private ThemeService()
    {
        Settings.Default.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.Default.Theme))
                Refresh();
        };

        // The OS theme and accent only matter in Auto mode, but refreshing unconditionally is cheap and keeps the logic simple.
        // The event can arrive on a non-UI thread, so marshal back to the dispatcher before touching bound state.
        SystemEvents.UserPreferenceChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);

        Refresh();
    }

    /// <summary>
    /// Whether the effective theme is dark. Light and Dark force the value; Auto follows the system.
    /// </summary>
    public bool IsDark { get; private set; }

    /// <summary>
    /// The system accent color used for the icons in Auto mode, or null when it can't be read (the monitors then keep their normal per-theme colors).
    /// </summary>
    public Brush AccentBrush { get; private set; }

    private void Refresh()
    {
        IsDark = Settings.Default.Theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => !ReadAppsUseLightTheme()
        };

        AccentBrush = ReadAccentColor() is Color accent ? CreateFrozenBrush(accent) : null;

        // Raise unconditionally: even when IsDark is unchanged the icons still need to switch between their palette and the accent when only the mode changed.
        RaisePropertyChanged(nameof(IsDark));
        RaisePropertyChanged(nameof(AccentBrush));
    }

    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            // Registry access can be denied in locked-down or partial-trust environments; default to light.
            return true;
        }
    }

    private static Color? ReadAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");

            // Stored as a COLORREF-style DWORD in ABGR order (0xAABBGGRR), so red is the low byte.
            if (key?.GetValue("AccentColor") is int stored)
            {
                var abgr = unchecked((uint)stored);
                return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
            }

            return null;
        }
        catch
        {
            // Registry access can be denied in locked-down or partial-trust environments; fall back to the per-theme icon colors.
            return null;
        }
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
