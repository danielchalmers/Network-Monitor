using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Network_Monitor;

/// <summary>
/// Puts the widget's size on a logarithmic scale, as DesktopClock does, so the Size slider and Ctrl+scroll take small steps at small sizes and bigger ones at big sizes.
/// </summary>
public class SizeScaleConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// How far one notch of the mouse wheel moves along the scale, about a 10% change in size.
    /// </summary>
    public const double StepSize = 0.1;

    public const int MinSize = 32;

    public const int MaxSize = 320;

    public static readonly double MinSizeLog = Math.Log(MinSize);

    public static readonly double MaxSizeLog = Math.Log(MaxSize);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ToLogSize(System.Convert.ToInt32(value, culture));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        FromLogSize(System.Convert.ToDouble(value, culture));

    /// <summary>
    /// Returns the size after scrolling by <paramref name="steps" /> notches of the mouse wheel, kept within the allowed range.
    /// </summary>
    public static int ScaleSize(int size, double steps) => FromLogSize(Math.Log(size) + (steps * StepSize));

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public static double ToLogSize(double size) => Math.Log(size);

    /// <summary>
    /// Returns the size at a point on the scale, kept within the allowed range.
    /// </summary>
    public static int FromLogSize(double logSize) =>
        (int)Math.Round(Math.Exp(Math.Min(Math.Max(logSize, MinSizeLog), MaxSizeLog)));
}
