using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
/// The colors of the widget, the menu, the tooltips, and the Ping address box come from a palette in Themes, which this puts in the app's resources in place of the one App.xaml starts with.
/// </remarks>
public sealed class ThemeService : ObservableObject
{
    /// <summary>
    /// The app resource for the accent of the menu and the Ping address box, like its check marks, which this sets to the system accent in Auto.
    /// The palettes have one of their own for the other themes.
    /// </summary>
    private const string AccentBrushKey = "AccentBrush";

    private static readonly Uri LightPaletteUri = new("Themes/LightPalette.xaml", UriKind.Relative);
    private static readonly Uri DarkPaletteUri = new("Themes/DarkPalette.xaml", UriKind.Relative);
    private static readonly Uri HighContrastPaletteUri = new("Themes/HighContrastPalette.xaml", UriKind.Relative);

    private Uri _paletteUri;

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
    /// The system accent color for the icons in Auto mode, in a shade that stands out from the widget's background, or null in the other modes, in high contrast, or when it can't be read (the monitors then keep their normal per-theme colors).
    /// </summary>
    public Brush WidgetAccentBrush { get; private set; }

    /// <summary>
    /// The system accent color for the graphs in Auto mode, in a shade that stands out from the tooltip's background, or null whenever <see cref="WidgetAccentBrush" /> is.
    /// </summary>
    public Brush ToolTipAccentBrush { get; private set; }

    /// <summary>
    /// Gives a window with a title bar, like the Ping address box, the theme's title bar and background from the moment it appears.
    /// Call it once the window has a handle, from <see cref="Window.SourceInitialized" />; the title bar follows the theme from then on.
    /// </summary>
    public void ApplyToWindow(Window window)
    {
        ApplyTitleBar(window);

        // Windows shows a new window before WPF has drawn anything in it, and it's white until then, which flashes in the dark theme, as DesktopClock found with its settings window.
        HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(EraseWithThemeBackground);
    }

    private void Refresh()
    {
        IsHighContrast = SystemParameters.HighContrast;

        IsDark = Settings.Default.Theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => !ReadAppsUseLightTheme()
        };

        var palette = ApplyPalette();

        // The accent stands in for the readings' own colors only in Auto, and a high contrast theme keeps to its own colors.
        var accent = Settings.Default.Theme == AppTheme.Auto && !IsHighContrast && palette is not null ? ReadAccentColor() : null;

        if (accent is Color color)
        {
            // The widget is see-through, so its background is checked as it looks over both black and white, the darkest and lightest the desktop behind it can be.
            var widget = (SolidColorBrush)palette["WidgetBackgroundBrush"];

            WidgetAccentBrush = CreateReadableBrush(color,
                ColorContrast.Over(widget.Color, widget.Opacity, Colors.Black),
                ColorContrast.Over(widget.Color, widget.Opacity, Colors.White));

            ToolTipAccentBrush = CreateReadableBrush(color, ColorOf(palette, "MenuBackgroundBrush"));

            // A check mark, or the slider's bar, on the menu or an item highlighted in it, and the borders of the Ping address box.
            Application.Current.Resources[AccentBrushKey] = CreateReadableBrush(color,
                ColorOf(palette, "MenuBackgroundBrush"),
                ColorOf(palette, "HoverBrush"),
                ColorOf(palette, "WindowBackgroundBrush"));
        }
        else
        {
            WidgetAccentBrush = null;
            ToolTipAccentBrush = null;
            Application.Current?.Resources.Remove(AccentBrushKey);
        }

        if (Application.Current is not null)
        {
            foreach (Window window in Application.Current.Windows)
                ApplyTitleBar(window);
        }

        // Raise unconditionally: even when IsDark is unchanged the icons still need to switch between their palette and the accent when only the mode changed.
        RaisePropertyChanged(nameof(IsDark));
        RaisePropertyChanged(nameof(IsHighContrast));
        RaisePropertyChanged(nameof(WidgetAccentBrush));
        RaisePropertyChanged(nameof(ToolTipAccentBrush));
    }

    /// <summary>
    /// Puts the palette for the theme in the app's resources, if it isn't there already, and returns it, or null when there's no app to put it in.
    /// </summary>
    private ResourceDictionary ApplyPalette()
    {
        if (Application.Current is null)
            return null;

        var uri = IsHighContrast ? HighContrastPaletteUri : IsDark ? DarkPaletteUri : LightPaletteUri;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(x => x.Source?.OriginalString.EndsWith("Palette.xaml", StringComparison.Ordinal) == true);

        // The high contrast palette is loaded afresh every time, since its colors are the system's, and this runs when they change.
        // Its brushes follow them on their own as well, but this way they can't miss a change WPF hadn't read yet.
        if (existing is not null && uri == _paletteUri && !IsHighContrast)
            return existing;

        var palette = new ResourceDictionary { Source = uri };

        if (existing is not null)
            dictionaries[dictionaries.IndexOf(existing)] = palette;
        else
            dictionaries.Add(palette);

        _paletteUri = uri;
        return palette;
    }

    private void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        // The widget has no title bar, and a window that hasn't been shown yet has no handle; it's done when it's first shown.
        if (hwnd == IntPtr.Zero || window.WindowStyle == WindowStyle.None)
            return;

        // Windows versions before 20H1 used another number for this, but .NET Framework 4.8.1 doesn't run on them.
        // A high contrast theme draws its own title bars.
        var useDarkMode = IsDark && !IsHighContrast ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }

    private static IntPtr EraseWithThemeBackground(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_ERASEBKGND)
            return IntPtr.Zero;

        // Read when asked, so a window that's open when the theme changes erases with the new color.
        if (Application.Current?.TryFindResource("WindowBackgroundBrush") is not SolidColorBrush background || !NativeMethods.GetClientRect(hwnd, out var rect))
            return IntPtr.Zero;

        // A COLORREF is 0x00BBGGRR.
        var gdiBrush = NativeMethods.CreateSolidBrush((uint)(background.Color.R | (background.Color.G << 8) | (background.Color.B << 16)));

        if (gdiBrush == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            // The message's wParam is the window's device context.
            NativeMethods.FillRect(wParam, ref rect, gdiBrush);
        }
        finally
        {
            NativeMethods.DeleteObject(gdiBrush);
        }

        handled = true;
        return new IntPtr(1);
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

    private static Color ColorOf(ResourceDictionary palette, string key) => ((SolidColorBrush)palette[key]).Color;

    /// <summary>
    /// The color in a shade that has enough contrast with each background to be read on it (see <see cref="ColorContrast.MinimumRatio" />), as a brush that can be shared.
    /// </summary>
    private static Brush CreateReadableBrush(Color color, params Color[] backgrounds)
    {
        var brush = new SolidColorBrush(ColorContrast.Readable(color, ColorContrast.MinimumRatio, backgrounds));
        brush.Freeze();
        return brush;
    }

    private static class NativeMethods
    {
        public const int WM_ERASEBKGND = 0x0014;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateSolidBrush(uint color);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr gdiObject);
    }
}
