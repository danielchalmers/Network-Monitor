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

    public static readonly DependencyProperty MinimumTopProperty = DependencyProperty.Register(
        nameof(MinimumTop), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

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
    /// The lowest value the top of the plot can stand for, so small values aren't zoomed in on (see <see cref="SparklineScale.For" />).
    /// </summary>
    public double MinimumTop
    {
        get => (double)GetValue(MinimumTopProperty);
        set => SetValue(MinimumTopProperty, value);
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

        var scale = SparklineScale.For(present, MinimumTop);

        double X(int index) => values.Count == 1 ? ActualWidth / 2 : (double)index / (values.Count - 1) * ActualWidth;

        // Higher values sit higher on screen.
        double Y(double value) => padding + (plotHeight * (1 - scale.GetHeight(value)));

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
