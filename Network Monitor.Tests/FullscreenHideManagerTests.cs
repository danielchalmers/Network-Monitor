using System.Windows;

namespace Network_Monitor.Tests;

public class FullscreenHideManagerTests
{
    private static readonly Rect Screen = new(0, 0, 2560, 1440);
    private static readonly Rect SecondScreen = new(2560, 0, 1920, 1080);

    [Fact]
    public void FillsMonitor_AWindowTheSizeOfTheMonitor_Fills()
    {
        Assert.True(FullscreenHideManager.FillsMonitor(Screen, Screen));
    }

    [Fact]
    public void FillsMonitor_AWindowReachingPastEveryEdge_Fills()
    {
        // Like a game that pushes its title bar off the top of the screen.
        Assert.True(FullscreenHideManager.FillsMonitor(new Rect(-8, -31, 2576, 1479), Screen));
    }

    [Theory]
    [InlineData(1, 0, 2559, 1440)] // A pixel short of the left.
    [InlineData(0, 1, 2560, 1439)] // A pixel short of the top.
    [InlineData(0, 0, 2559, 1440)] // A pixel short of the right.
    [InlineData(0, 0, 2560, 1439)] // A pixel short of the bottom.
    [InlineData(0, 0, 1280, 1440)] // Snapped to one half.
    [InlineData(0, 0, 2560, 1392)] // Maximized above a taskbar.
    public void FillsMonitor_AWindowShortOfAnEdge_DoesntFill(double left, double top, double width, double height)
    {
        Assert.False(FullscreenHideManager.FillsMonitor(new Rect(left, top, width, height), Screen));
    }

    [Fact]
    public void FillsMonitor_AFullscreenWindowOnAnotherMonitor_DoesntFillThisOne()
    {
        Assert.True(FullscreenHideManager.FillsMonitor(SecondScreen, SecondScreen));
        Assert.False(FullscreenHideManager.FillsMonitor(SecondScreen, Screen));
    }

    [Fact]
    public void FillsMonitor_AWindowAcrossBothMonitors_FillsEach()
    {
        var both = Rect.Union(Screen, SecondScreen);

        Assert.True(FullscreenHideManager.FillsMonitor(both, Screen));
        Assert.True(FullscreenHideManager.FillsMonitor(both, SecondScreen));
    }
}
