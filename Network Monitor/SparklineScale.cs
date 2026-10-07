using System.Collections.Generic;
using System.Linq;

namespace Network_Monitor;

/// <summary>
/// The vertical scale of a <see cref="Sparkline" />.
/// </summary>
public readonly struct SparklineScale
{
    private SparklineScale(double bottom, double top, bool baselineZero)
    {
        Bottom = bottom;
        Top = top;
        BaselineZero = baselineZero;
    }

    /// <summary>
    /// The value drawn at the bottom of the plot.
    /// </summary>
    public double Bottom { get; }

    /// <summary>
    /// The value drawn at the top of the plot.
    /// </summary>
    public double Top { get; }

    private bool BaselineZero { get; }

    /// <summary>
    /// Returns the scale for the given values: from zero when <paramref name="baselineZero" /> is set, otherwise spanning the values' own range.
    /// </summary>
    public static SparklineScale For(IEnumerable<double> values, bool baselineZero)
    {
        var list = values.ToList();

        return new SparklineScale(baselineZero ? 0 : list.Min(), list.Max(), baselineZero);
    }

    /// <summary>
    /// Returns how far up the plot a value sits, from 0 (bottom) to 1 (top).
    /// A series with no range sits on the midline, except an all-zero series on a zero baseline, which belongs on the floor.
    /// </summary>
    public double GetHeight(double value)
    {
        var range = Top - Bottom;

        if (range <= 0)
            return BaselineZero ? 0 : 0.5;

        return (value - Bottom) / range;
    }
}
