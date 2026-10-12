using System.Linq;
using System.Windows.Media;

namespace Network_Monitor.Tests;

public class ColorContrastTests
{
    // The backgrounds ThemeService checks the accent against, from Themes/LightPalette.xaml and Themes/DarkPalette.xaml.
    private const double WidgetOpacity = 0.90;
    private static readonly Color LightWidget = Hex("#F3F3F3");
    private static readonly Color DarkWidget = Hex("#202020");
    private static readonly Color LightToolTip = Hex("#F9F9F9");
    private static readonly Color DarkToolTip = Hex("#2C2C2C");

    // The menu, an item highlighted in it, and the Ping address box.
    private static readonly Color[] LightMenu = { Hex("#F9F9F9"), Hex("#EAEAEA"), Hex("#F3F3F3") };
    private static readonly Color[] DarkMenu = { Hex("#2C2C2C"), Hex("#383838"), Hex("#202020") };

    [Theory]
    [InlineData("#000000", "#FFFFFF", 21)]
    [InlineData("#FFFFFF", "#000000", 21)]
    [InlineData("#767676", "#FFFFFF", 4.54)] // The lightest gray that has 4.5 to 1 on white.
    [InlineData("#FF0000", "#FFFFFF", 4.00)] // Green counts for most of how light a color looks, and blue least.
    [InlineData("#00FF00", "#000000", 15.30)]
    [InlineData("#0000FF", "#FFFFFF", 8.59)]
    [InlineData("#0078D4", "#0078D4", 1)]
    public void Ratio_MatchesWcag(string a, string b, double expected)
    {
        Assert.Equal(expected, ColorContrast.Ratio(Hex(a), Hex(b)), 2);
    }

    [Fact]
    public void Over_BlendsTheSeeThroughWidgetWithWhatsBehindIt()
    {
        Assert.Equal(Hex("#1D1D1D"), ColorContrast.Over(DarkWidget, WidgetOpacity, Colors.Black));
        Assert.Equal(Hex("#363636"), ColorContrast.Over(DarkWidget, WidgetOpacity, Colors.White));
        Assert.Equal(Hex("#DBDBDB"), ColorContrast.Over(LightWidget, WidgetOpacity, Colors.Black));
        Assert.Equal(Hex("#F4F4F4"), ColorContrast.Over(LightWidget, WidgetOpacity, Colors.White));
    }

    [Fact]
    public void Readable_StandsOutJustEnoughWhateverTheAccent()
    {
        var surfaces = new[]
        {
            WidgetOverAnything(LightWidget),
            WidgetOverAnything(DarkWidget),
            new[] { LightToolTip },
            new[] { DarkToolTip },
            LightMenu,
            DarkMenu,
        };

        // Every 17th shade of each channel, which takes in black, white, grays, and the pure colors.
        for (var r = 0; r <= 255; r += 17)
        {
            for (var g = 0; g <= 255; g += 17)
            {
                for (var b = 0; b <= 255; b += 17)
                {
                    var accent = Color.FromRgb((byte)r, (byte)g, (byte)b);

                    foreach (var backgrounds in surfaces)
                    {
                        var shade = ColorContrast.Readable(accent, ColorContrast.MinimumRatio, backgrounds);
                        var ratio = backgrounds.Min(x => ColorContrast.Ratio(shade, x));

                        Assert.True(ratio >= ColorContrast.MinimumRatio, $"{accent} became {shade}, which has {ratio:0.00} to 1 on {backgrounds[0]}");

                        // One that had to change should only just stand out, so it's as close to the accent as it can be.
                        if (shade != accent)
                            Assert.True(ratio < ColorContrast.MinimumRatio + 0.25, $"{accent} became {shade}, which has {ratio:0.00} to 1 on {backgrounds[0]}");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("#FFB900", "#202020")] // Gold in the dark theme.
    [InlineData("#3B003B", "#F3F3F3")] // Plum in the light theme.
    [InlineData("#FFFFFF", "#202020")]
    [InlineData("#000000", "#F3F3F3")]
    public void Readable_KeepsAnAccentThatAlreadyStandsOut(string accent, string widget)
    {
        Assert.Equal(Hex(accent), ColorContrast.Readable(Hex(accent), ColorContrast.MinimumRatio, WidgetOverAnything(Hex(widget))));
    }

    [Fact]
    public void Readable_LightensADarkAccentInTheDarkTheme()
    {
        // Plum has almost no contrast with the dark theme's background.
        var plum = ColorContrast.Readable(Hex("#3B003B"), ColorContrast.MinimumRatio, WidgetOverAnything(DarkWidget));

        // Lighter, and still as much red as blue with less green, so it's still plum.
        Assert.True(plum.R > 0x3B && plum.G > 0x00, plum.ToString());
        Assert.Equal(plum.R, plum.B);
        Assert.True(plum.G < plum.R, plum.ToString());
    }

    [Fact]
    public void Readable_DarkensALightAccentInTheLightTheme()
    {
        // Gold has almost no contrast with the light theme's background.
        var gold = ColorContrast.Readable(Hex("#FFB900"), ColorContrast.MinimumRatio, WidgetOverAnything(LightWidget));

        // Darker, and still more red than green with no blue, so it's still gold.
        Assert.True(gold.R < 0xFF && gold.G < 0xB9, gold.ToString());
        Assert.Equal(0, gold.B);
        Assert.True(gold.G < gold.R, gold.ToString());
    }

    [Fact]
    public void Readable_ChangesTheAccentNoMoreThanItNeeds()
    {
        // Windows' default blue is a little dark for the dark theme, and should only be lightened a little, not washed out toward white.
        var backgrounds = WidgetOverAnything(DarkWidget);
        var blue = ColorContrast.Readable(Hex("#0078D4"), ColorContrast.MinimumRatio, backgrounds);

        Assert.InRange(backgrounds.Min(x => ColorContrast.Ratio(blue, x)), ColorContrast.MinimumRatio, ColorContrast.MinimumRatio + 0.25);
        Assert.True(blue.B > blue.G && blue.G > blue.R, blue.ToString());
    }

    [Fact]
    public void Readable_WhenNothingStandsOutEnough_GivesWhiteOrBlack()
    {
        // No color has more than 21 to 1 with anything.
        Assert.Equal(Colors.White, ColorContrast.Readable(Hex("#0078D4"), 25, DarkWidget));
        Assert.Equal(Colors.Black, ColorContrast.Readable(Hex("#0078D4"), 25, LightWidget));
    }

    /// <summary>
    /// The widget's background over black and over white, the darkest and lightest it can look over the desktop, as ThemeService checks it.
    /// </summary>
    private static Color[] WidgetOverAnything(Color widget) =>
        new[] { ColorContrast.Over(widget, WidgetOpacity, Colors.Black), ColorContrast.Over(widget, WidgetOpacity, Colors.White) };

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
