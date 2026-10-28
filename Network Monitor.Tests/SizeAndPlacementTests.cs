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

    [Fact]
    public void GetPositionAfterResize_InTheBottomRightAt150Percent_StaysInTheCorner()
    {
        // At 150%, a widget of 128 by 138 units is 192 by 207 pixels, and it grows to 160 by 172.67 units, which WPF lays out in whole pixels.
        var dpi = new DpiScale(1.5, 1.5);
        var oldBounds = new Rect(new Point(2368, 1233), ScreenEdges.ToDevicePixels(new Size(128, 138), dpi));

        var position = Resize(oldBounds, ScreenEdges.ToDevicePixels(new Size(160, 259 / 1.5), dpi));

        Assert.Equal(new Point(2320, 1181), position);
    }

    [Theory]
    [InlineData(1.0, 128, 138)]
    [InlineData(1.5, 192, 207)]
    [InlineData(2.0, 256, 276)]
    public void ToDevicePixels_ScalesTheSize(double scale, double width, double height)
    {
        Assert.Equal(new Size(width, height), ScreenEdges.ToDevicePixels(new Size(128, 138), new DpiScale(scale, scale)));
    }

    [Fact]
    public void ToDevicePixels_AtAScaleThatDoesNotDivideEvenly_GivesWholePixels()
    {
        // At 175%, 115 pixels is about 65.71 units, which multiplies back to 114.99999999999999.
        var size = ScreenEdges.ToDevicePixels(new Size(115 / 1.75, 122 / 1.75), new DpiScale(1.75, 1.75));

        Assert.Equal(new Size(115, 122), size);
    }

    [Fact]
    public void GetBoundsAfterScaleChange_InTheBottomRight_StaysInTheCorner()
    {
        // The monitor's scale goes from 100% to 150%.
        var bounds = ScreenEdges.GetBoundsAfterScaleChange(new Rect(2432, 1302, 128, 138), new Size(192, 207), Screen);

        Assert.Equal(new Rect(2368, 1233, 192, 207), bounds);
    }

    [Fact]
    public void GetBoundsAfterScaleChange_InTheTopLeft_KeepsItsTopLeftCorner()
    {
        var bounds = ScreenEdges.GetBoundsAfterScaleChange(new Rect(100, 100, 128, 138), new Size(192, 207), Screen);

        Assert.Equal(new Rect(100, 100, 192, 207), bounds);
    }

    [Fact]
    public void GetBoundsAfterScaleChange_OnAMonitorLeftOfTheMainOne_StaysInTheCorner()
    {
        // A monitor to the left of the main one has negative coordinates. Its scale goes from 150% to 100%, so the widget shrinks.
        var monitor = new Rect(-1920, 0, 1920, 1080);

        var bounds = ScreenEdges.GetBoundsAfterScaleChange(new Rect(-192, 873, 192, 207), new Size(128, 138), monitor);

        Assert.Equal(new Rect(-128, 942, 128, 138), bounds);
    }

    [Fact]
    public void GetBoundsAfterScaleChange_PartlyOffTheMonitor_ComesFullyOntoIt()
    {
        var bounds = ScreenEdges.GetBoundsAfterScaleChange(new Rect(2500, 600, 128, 138), new Size(256, 276), Screen);

        Assert.Equal(new Rect(2304, 600, 256, 276), bounds);
    }

    [Fact]
    public void GetBoundsAfterScaleChange_PastTheEdgeOfAMonitorScaledLowerThanTheMainOne_ComesFullyOntoIt()
    {
        // 4.x saved positions at the main monitor's scale, 150% here, which stretched this 100% monitor to half again its size, so its widget could be saved past its right edge.
        var monitor = new Rect(2880, 0, 1920, 1080);

        var bounds = ScreenEdges.GetBoundsAfterScaleChange(new Rect(4700, 900, 192, 207), new Size(128, 138), monitor);

        Assert.Equal(new Rect(4672, 942, 128, 138), bounds);
    }

    private static Point Resize(Rect oldBounds, Size newSize) =>
        ScreenEdges.GetPositionAfterResize(oldBounds, newSize, Screen, ScreenEdges.GetFixedEdges(oldBounds, Screen));
}
