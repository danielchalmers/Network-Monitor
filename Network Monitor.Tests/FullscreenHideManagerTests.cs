using System.Windows;

namespace Network_Monitor.Tests;

public class FullscreenHideManagerTests
{
    private static readonly Rect Screen = new(0, 0, 2560, 1440);

    [Theory]
    [InlineData(0, 0, 2560, 1440, true)] // The size of the monitor.
    [InlineData(-8, -31, 2576, 1479, true)] // Past every edge, like a game that pushes its title bar off the top of the screen.
    [InlineData(1, 0, 2559, 1440, false)] // A pixel short of the left.
    [InlineData(0, 0, 2560, 1392, false)] // Maximized above a taskbar.
    [InlineData(2560, 0, 1920, 1080, false)] // Fullscreen on another monitor.
    public void FillsMonitor_OnlyWhenItReachesEveryEdge(double left, double top, double width, double height, bool expected)
    {
        Assert.Equal(expected, FullscreenHideManager.FillsMonitor(new Rect(left, top, width, height), Screen));
    }
}
