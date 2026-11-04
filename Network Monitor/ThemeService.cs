using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Single source of truth for the effective theme.
/// Light and Dark force the appearance; Auto follows the operating system's light/dark setting and accent color, updating live when either changes.
/// A high contrast theme overrides all three with its own colors, whenever it's on.
/// </summary>
/// <remarks>
/// The colors of the widget, the menu, and the tooltips come from a palette in Themes, which this puts in the app's resources in place of the one App.xaml starts with.
/// </remarks>
public sealed class ThemeService : ObservableObject
{
    /// <summary>
    /// The app resource for the accent of the menu, like its check marks, which this sets to the system accent in Auto.
    /// The palettes have one of their own for the other themes.
    /// </summary>
    private const string AccentBrushKey = "AccentBrush";

    private static readonly Uri LightPaletteUri = new("Themes/LightPalette.xaml", UriKind.Relative);
    private static readonly Uri DarkPaletteUri = new("Themes/DarkPalette.xaml", UriKind.Relative);
    private static readonly Uri HighContrastPaletteUri = new("Themes/HighContrastPalette.xaml", UriKind.Relative);

    // After the palettes' addresses, since static fields are set in the order they're written, and creating this reads them.
    public static ThemeService Instance { get; } = new();

    private ThemeService()
    {
        Settings.Default.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.Default.Theme))
                Refresh();
        };

        // The OS theme and accent only matter in Auto mode, and the system's colors only in high contrast, but refreshing unconditionally is cheap and keeps the logic simple.
        // The event can arrive on a non-UI thread, so marshal back to the dispatcher before touching bound state.
        SystemEvents.UserPreferenceChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);

        // High contrast is also announced by WPF once it has read the new setting, which the event above can arrive before.
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
                Application.Current?.Dispatcher.BeginInvoke(Refresh);
        };

        Refresh();
    }

    /// <summary>
    /// Whether the effective theme is dark. Light and Dark force the value; Auto follows the system.
    /// </summary>
    public bool IsDark { get; private set; }

    /// <summary>
    /// Whether a high contrast theme is on, in which only its own colors are sure to be readable, so they're used for everything and nothing is see-through.
    /// </summary>
    public bool IsHighContrast { get; private set; }

    /// <summary>
    /// The system accent color for the icons, the graphs, and the menu in Auto mode, in the shade Windows uses for accent-colored text in the theme, or null in the other modes, in high contrast, or when it can't be read (the monitors then keep their normal per-theme colors).
    /// </summary>
    public Brush AccentBrush { get; private set; }

    private void Refresh()
    {
        IsHighContrast = SystemParameters.HighContrast;

        IsDark = Settings.Default.Theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => !ReadAppsUseLightTheme()
        };

        // The accent stands in for the readings' own colors only in Auto, and a high contrast theme keeps to its own colors.
        var accent = Settings.Default.Theme == AppTheme.Auto && !IsHighContrast ? ReadAccentColor(IsDark) : null;
        AccentBrush = accent is Color color ? CreateFrozenBrush(color) : null;

        if (Application.Current is not null)
        {
            ApplyPalette();

            // The menu's check marks and the Size slider take the accent in place of the palette's own.
            if (AccentBrush is not null)
                Application.Current.Resources[AccentBrushKey] = AccentBrush;
            else
                Application.Current.Resources.Remove(AccentBrushKey);
        }

        // Raise unconditionally: even when IsDark is unchanged the icons still need to switch between their palette and the accent when only the mode changed.
        RaisePropertyChanged(nameof(IsDark));
        RaisePropertyChanged(nameof(IsHighContrast));
        RaisePropertyChanged(nameof(AccentBrush));
    }

    /// <summary>
    /// Puts the palette for the theme in the app's resources, in place of the one that's there.
    /// </summary>
    private void ApplyPalette()
    {
        var uri = IsHighContrast ? HighContrastPaletteUri : IsDark ? DarkPaletteUri : LightPaletteUri;

        // The palette is the second of App.xaml's dictionaries, after FluentTheme.xaml.
        Application.Current.Resources.MergedDictionaries[1] = new ResourceDictionary { Source = uri };
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

    /// <summary>
    /// The system accent color in the shade Windows uses for accent-colored text and icons in the theme, or the accent itself when Windows hasn't saved its shades.
    /// </summary>
    private static Color? ReadAccentColor(bool isDark)
    {
        try
        {
            using var accent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");

            // Eight colors of four bytes each, red first: Light3, Light2, Light1, the accent, Dark1, Dark2, Dark3, and one that's unused.
            // WinUI uses Light3 for accent-colored text and icons in the dark theme, and Dark2 in the light one.
            if (accent?.GetValue("AccentPalette") is byte[] { Length: >= 32 } palette)
            {
                var offset = isDark ? 0 : 20;
                return Color.FromRgb(palette[offset], palette[offset + 1], palette[offset + 2]);
            }

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
