using System;
using System.Collections.Generic;
using System.Linq;

namespace Network_Monitor;

/// <summary>
/// The vertical scale of a <see cref="Sparkline" />, which always starts at zero so the line shows how big values are.
/// </summary>
public readonly struct SparklineScale
{
    private SparklineScale(double top)
    {
        Top = top;
    }

    /// <summary>
    /// The value drawn at the top of the plot.
    /// </summary>
    public double Top { get; }

    /// <summary>
    /// Returns the scale for the given values: from zero to the largest of them, but never zoomed in tighter than zero to <paramref name="minimumTop" />.
    /// The minimum keeps small wobbles small; a healthy 15–31 ms of latency would otherwise fill the whole height and look as alarming as a real spike.
    /// </summary>
    public static SparklineScale For(IEnumerable<double> values, double minimumTop) =>
        new(Math.Max(values.DefaultIfEmpty(0).Max(), minimumTop));

    /// <summary>
    /// Returns how far up the plot a value sits, from 0 (bottom) to 1 (top).
    /// With nothing above zero to scale against, everything sits on the floor.
    /// </summary>
    public double GetHeight(double value) => Top <= 0 ? 0 : value / Top;
}
