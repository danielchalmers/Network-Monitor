using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Network_Monitor;

/// <summary>
/// A lightweight sparkline that draws a normalized line across a sequence of values.
/// Missing values (null) break the line into gaps, so a lost ping shows as a visible break rather than a dip to zero.
/// </summary>
public class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double?>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BaselineZeroProperty = DependencyProperty.Register(
        nameof(BaselineZero), typeof(bool), typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// The values to plot, oldest first. Null entries are gaps.
    /// </summary>
    public IReadOnlyList<double?> Values
    {
        get => (IReadOnlyList<double?>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>
    /// Whether the vertical scale is anchored at zero so the line shows magnitude, like a throughput graph.
    /// When false the scale spans the data's own min–max range, which emphasizes variation, like jitter in a latency graph.
    /// </summary>
    public bool BaselineZero
    {
        get => (bool)GetValue(BaselineZeroProperty);
        set => SetValue(BaselineZeroProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var values = Values;

        if (values is null || values.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var present = values.Where(v => v.HasValue).Select(v => v.Value).ToArray();

        if (present.Length == 0)
            return;

        // Pad vertically so the line and its rounded caps aren't clipped at the top and bottom extremes.
        var padding = StrokeThickness + 1;
        var plotHeight = Math.Max(1, ActualHeight - (2 * padding));

        var min = BaselineZero ? 0 : present.Min();
        var max = present.Max();
        var range = max - min;

        double X(int index) => values.Count == 1 ? ActualWidth / 2 : (double)index / (values.Count - 1) * ActualWidth;

        // Normalize over the vertical range; higher values sit higher on screen.
        // A series with no range sits on the midline, except an all-zero series on a zero baseline, which belongs on the floor.
        double Y(double value) => range <= 0
            ? padding + (BaselineZero ? plotHeight : plotHeight / 2)
            : padding + (plotHeight * (1 - ((value - min) / range)));

        var pen = new Pen(Stroke, StrokeThickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze();

        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            var figureOpen = false;

            for (var i = 0; i < values.Count; i++)
            {
                if (!values[i].HasValue)
                {
                    figureOpen = false; // Break the line across a gap (e.g. a lost ping).
                    continue;
                }

                var point = new Point(X(i), Y(values[i].Value));

                if (figureOpen)
                {
                    context.LineTo(point, true, false);
                }
                else
                {
                    context.BeginFigure(point, false, false);
                    figureOpen = true;
                }

                // A point with gaps on both sides has no segment to stroke, so give it a dot; otherwise it would silently vanish from the line.
                if (!HasValueAt(values, i - 1) && !HasValueAt(values, i + 1))
                    drawingContext.DrawEllipse(Stroke, null, point, StrokeThickness * 0.75, StrokeThickness * 0.75);
            }
        }

        geometry.Freeze();

        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private static bool HasValueAt(IReadOnlyList<double?> values, int index) =>
        index >= 0 && index < values.Count && values[index].HasValue;
}
