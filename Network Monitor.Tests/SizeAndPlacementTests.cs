using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Network_Monitor.Tests;

public class SizeAndPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 2560, 1440);

    [Theory]
    [InlineData(32)]
    [InlineData(96)]
    [InlineData(320)]
    public void SizeScaleConverter_RoundTripsASize(int size)
    {
        var converter = new SizeScaleConverter();
        var logSize = converter.Convert(size, typeof(double), null, CultureInfo.InvariantCulture);

        Assert.Equal(size, converter.ConvertBack(logSize, typeof(int), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ScaleSize_OneNotchIsAboutTenPercent()
    {
        Assert.Equal(106, SizeScaleConverter.ScaleSize(96, 1));
        Assert.Equal(87, SizeScaleConverter.ScaleSize(96, -1));
    }

    [Fact]
    public void ScaleSize_StaysWithinTheLimits()
    {
        Assert.Equal(SizeScaleConverter.MaxSize, SizeScaleConverter.ScaleSize(300, 10));
        Assert.Equal(SizeScaleConverter.MinSize, SizeScaleConverter.ScaleSize(40, -10));
    }

    [Fact]
    public void ScaleSize_UpThenDownComesBackToTheSameSize()
    {
        var size = Enumerable.Range(0, 5).Aggregate(96, (s, _) => SizeScaleConverter.ScaleSize(s, 1));
        size = Enumerable.Range(0, 5).Aggregate(size, (s, _) => SizeScaleConverter.ScaleSize(s, -1));

        Assert.InRange(size, 95, 97);
    }

    [Fact]
    public void Slider_HasFineStepsAtSmallSizes()
    {
        // The 80 px slider spans the log range, so its first tenth stays near the minimum instead of jumping to a third of the range.
        var tenth = SizeScaleConverter.MinSizeLog + ((SizeScaleConverter.MaxSizeLog - SizeScaleConverter.MinSizeLog) / 10);
        var size = (int)new SizeScaleConverter().ConvertBack(tenth, typeof(int), null, CultureInfo.InvariantCulture);

        Assert.InRange(size, 32, 40);
    }

    [Fact]
    public void SizeSlider_UsesTheSameScaleAsTheWheel()
    {
        Assert.Equal(SizeScaleConverter.ScaleSize(96, 1), SizeScaleConverter.FromLogSize(SizeScaleConverter.ToLogSize(96) + SizeScaleConverter.StepSize));
    }

    [Fact]
    public void GetPositionAfterResize_InTheTopLeft_GrowsDownAndRight()
    {
        var position = Resize(new Rect(100, 100, 128, 138), new Size(200, 216));

        Assert.Equal(new Point(100, 100), position);
    }

    [Fact]
    public void GetPositionAfterResize_InTheBottomRight_GrowsUpAndLeft()
    {
        var position = Resize(new Rect(2432, 1254, 128, 138), new Size(200, 216));

        Assert.Equal(new Point(2360, 1176), position);
    }

    [Fact]
    public void GetPositionAfterResize_SwitchingFromAStripToAColumnInTheCorner_StaysOnScreen()
    {
        var strip = new Rect(2160, 1344, 400, 48);

        var position = Resize(strip, new Size(128, 138));

        Assert.Equal(new Point(2432, 1254), position);
    }

    [Fact]
    public void GetPositionAfterResize_GrowingAcrossTheMiddleAndBack_ReturnsToWhereItStarted()
    {
        // Just left of the middle, so growing pushes the window's center into the right half partway through.
        var start = new Rect(1180, 600, 128, 138);
        var edges = ScreenEdges.GetFixedEdges(start, Screen);
        var bounds = start;

        foreach (var size in new[] { 160, 200, 260, 320, 260, 200, 160, 128 })
        {
            var newSize = new Size(size, size * 138 / 128);
            bounds = new Rect(ScreenEdges.GetPositionAfterResize(bounds, newSize, Screen, edges), newSize);
        }

        Assert.Equal(start.TopLeft, bounds.TopLeft);
    }

    [Theory]
    [InlineData(100, 100, ScreenEdges.FixedEdges.TopLeft)]
    [InlineData(2400, 100, ScreenEdges.FixedEdges.Right)]
    [InlineData(100, 1300, ScreenEdges.FixedEdges.Bottom)]
    [InlineData(2400, 1300, ScreenEdges.FixedEdges.Right | ScreenEdges.FixedEdges.Bottom)]
    public void GetFixedEdges_KeepsTheEdgesNearestTheScreenSides(double left, double top, ScreenEdges.FixedEdges expected)
    {
        Assert.Equal(expected, ScreenEdges.GetFixedEdges(new Rect(left, top, 128, 138), Screen));
    }

    [Fact]
    public void KeepOnScreen_PullsAWindowBackOntoTheScreen()
    {
        Assert.Equal(new Point(2432, 0), ScreenEdges.KeepOnScreen(new Rect(2500, -20, 128, 138), Screen));
    }

    private static Point Resize(Rect oldBounds, Size newSize) =>
        ScreenEdges.GetPositionAfterResize(oldBounds, newSize, Screen, ScreenEdges.GetFixedEdges(oldBounds, Screen));
}
