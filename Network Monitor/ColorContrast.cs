using System;
using System.Linq;
using System.Windows.Media;

namespace Network_Monitor;

/// <summary>
/// Keeps a color readable on what it's drawn on, by WCAG's measure of contrast, as DesktopClock does for the text of its clock.
/// Windows lets the accent be any color, so in Auto a dark accent like plum would all but vanish on the dark theme's background, and a light one like yellow on the light theme's.
/// </summary>
public static class ColorContrast
{
    /// <summary>
    /// The least contrast the accent is given, as the readings' icons, the tooltip's graph, or the menu's check marks: 4.5 to 1, what WCAG asks of text, which the icons are.
    /// It's more than the 3 to 1 WCAG asks of graphics, so the graph's thin line stands out as well, and a little less than Windows' own blues that Light and Dark use instead have.
    /// </summary>
    public const double MinimumRatio = 4.5;

    /// <summary>
    /// How many steps toward white or black <see cref="Readable" /> takes at most, so a shade is never more than a hundredth of the way past the one it needs.
    /// </summary>
    private const int Steps = 100;

    /// <summary>
    /// The WCAG contrast ratio between two colors, from 1 for the same color to 21 for black and white.
    /// </summary>
    public static double Ratio(Color a, Color b)
    {
        var (lighter, darker) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// How a background drawn at the given opacity looks over what's behind it, such as the see-through widget over the desktop.
    /// </summary>
    public static Color Over(Color background, double opacity, Color backdrop) => Mix(backdrop, background, opacity);

    /// <summary>
    /// The color itself when it stands out enough from every background, otherwise the nearest shade of it that does.
    /// </summary>
    /// <remarks>
    /// The shade is mixed toward white or black, whichever stands out more from the backgrounds: lighter on the dark theme and darker on the light one.
    /// That keeps its hue, and always gets there for any contrast white or black can reach, since that end stands out most of all.
    /// </remarks>
    public static Color Readable(Color color, double minimumRatio, params Color[] backgrounds)
    {
        var target = LeastRatio(Colors.White, backgrounds) >= LeastRatio(Colors.Black, backgrounds) ? Colors.White : Colors.Black;

        for (var step = 0; step <= Steps; step++)
        {
            var shade = Mix(color, target, (double)step / Steps);

            if (LeastRatio(shade, backgrounds) >= minimumRatio)
                return shade;
        }

        return target;
    }

    private static double LeastRatio(Color color, Color[] backgrounds) =>
        backgrounds.Length == 0 ? double.MaxValue : backgrounds.Min(background => Ratio(color, background));

    /// <summary>
    /// The relative luminance WCAG defines, from 0 for black to 1 for white.
    /// </summary>
    private static double Luminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// The opaque color the given amount of the way from one color to another, from 0 for the first to 1 for the second.
    /// </summary>
    private static Color Mix(Color from, Color to, double amount) =>
        Color.FromRgb(Mix(from.R, to.R, amount), Mix(from.G, to.G, amount), Mix(from.B, to.B, amount));

    private static byte Mix(byte from, byte to, double amount) => (byte)Math.Round(from + ((to - from) * amount));
}
