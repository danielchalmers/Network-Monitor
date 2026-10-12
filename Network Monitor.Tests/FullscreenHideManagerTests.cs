using System;
using System.Windows;

namespace Network_Monitor.Tests;

public class FullscreenHideManagerTests
{
    private static readonly Rect Screen = new(0, 0, 2560, 1440);
    private static readonly Rect SecondScreen = new(2560, 0, 1920, 1080);

    private static readonly IntPtr Video = new(0x1001);
    private static readonly IntPtr Game = new(0x1002);
    private static readonly IntPtr Editor = new(0x1003);
    private static readonly IntPtr Slideshow = new(0x1004);
    private static readonly IntPtr PresenterView = new(0x1005);
    private static readonly IntPtr Desktop = new(0x2001);

    [Fact]
    public void FillsMonitor_AWindowTheSizeOfTheMonitor_Fills()
    {
        Assert.True(FullscreenHideManager.FillsMonitor(Screen, Screen, isMaximizedWithTitleBar: false));
    }

    [Fact]
    public void FillsMonitor_AWindowReachingPastEveryEdge_Fills()
    {
        // Like a game that pushes its title bar off the top of the screen.
        Assert.True(FullscreenHideManager.FillsMonitor(new Rect(-8, -31, 2576, 1479), Screen, isMaximizedWithTitleBar: false));
    }

    [Fact]
    public void FillsMonitor_AMaximizedWindowOnAMonitorWithoutATaskbar_DoesntFill()
    {
        // Its borders hang 8 pixels past every edge at 100% scaling, but it isn't fullscreen.
        Assert.False(FullscreenHideManager.FillsMonitor(new Rect(-8, -8, 2576, 1456), Screen, isMaximizedWithTitleBar: true));
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
        Assert.False(FullscreenHideManager.FillsMonitor(new Rect(left, top, width, height), Screen, isMaximizedWithTitleBar: false));
    }

    [Fact]
    public void FillsMonitor_AFullscreenWindowOnAnotherMonitor_DoesntFillThisOne()
    {
        Assert.True(FullscreenHideManager.FillsMonitor(SecondScreen, SecondScreen, isMaximizedWithTitleBar: false));
        Assert.False(FullscreenHideManager.FillsMonitor(SecondScreen, Screen, isMaximizedWithTitleBar: false));
    }

    [Fact]
    public void FillsMonitor_AWindowAcrossBothMonitors_FillsEach()
    {
        var both = Rect.Union(Screen, SecondScreen);

        Assert.True(FullscreenHideManager.FillsMonitor(both, Screen, isMaximizedWithTitleBar: false));
        Assert.True(FullscreenHideManager.FillsMonitor(both, SecondScreen, isMaximizedWithTitleBar: false));
    }

    [Theory]
    [InlineData(true, true)] // The desktop, which fills the monitor whenever nothing else is in front.
    [InlineData(false, false)] // A window that can't be seen, like a fullscreen video on another virtual desktop, which keeps its size there.
    public void Classify_AWindowThatFillsTheMonitorButDoesntCount_IsIgnored(bool isShowing, bool isOwnOrShell)
    {
        Assert.Equal(FullscreenHideManager.FrontWindow.Ignored, FullscreenHideManager.Classify(isShowing, isOwnOrShell, fillsMonitor: true, isOnMonitor: true));
    }

    [Fact]
    public void Classify_AGameAcrossEveryMonitor_IsFullscreenThoughItsCountedOnAnother()
    {
        // Windows counts a window as on whichever monitor holds the most of it, which needn't be the widget's.
        Assert.Equal(FullscreenHideManager.FrontWindow.Fullscreen, FullscreenHideManager.Classify(isShowing: true, isOwnOrShell: false, fillsMonitor: true, isOnMonitor: false));
    }

    [Fact]
    public void Classify_AWindowThatDoesntFillTheMonitor_IsWindowedOnItAndElsewhereOnAnother()
    {
        Assert.Equal(FullscreenHideManager.FrontWindow.Windowed, FullscreenHideManager.Classify(isShowing: true, isOwnOrShell: false, fillsMonitor: false, isOnMonitor: true));
        Assert.Equal(FullscreenHideManager.FrontWindow.Elsewhere, FullscreenHideManager.Classify(isShowing: true, isOwnOrShell: false, fillsMonitor: false, isOnMonitor: false));
    }

    [Fact]
    public void GetWindowToHideFor_AFullscreenAppInFront_HidesForIt()
    {
        Assert.Equal(Video, FullscreenHideManager.GetWindowToHideFor(Video, FullscreenHideManager.FrontWindow.Fullscreen, IntPtr.Zero, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_GoingFromOneFullscreenAppToAnother_HidesForTheNewOne()
    {
        Assert.Equal(Game, FullscreenHideManager.GetWindowToHideFor(Game, FullscreenHideManager.FrontWindow.Fullscreen, Video, FullscreenHideManager.HiddenForWindow.StillFills, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_AnotherWindowInFrontOnTheSameMonitor_Shows()
    {
        // Brought in front of a video that's still fullscreen behind it, and may still be on top of the rest of the monitor.
        Assert.Equal(IntPtr.Zero, FullscreenHideManager.GetWindowToHideFor(Editor, FullscreenHideManager.FrontWindow.Windowed, Video, FullscreenHideManager.HiddenForWindow.StillFills, fullscreenOnTop: Video));
    }

    [Fact]
    public void GetWindowToHideFor_WorkingOnAnotherMonitor_StaysHiddenWhileTheVideoIsFullscreen()
    {
        Assert.Equal(Video, FullscreenHideManager.GetWindowToHideFor(Editor, FullscreenHideManager.FrontWindow.Elsewhere, Video, FullscreenHideManager.HiddenForWindow.StillFills, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_WorkingOnAnotherMonitor_ShowsOnceTheVideoIsntFullscreen()
    {
        // Like a video that's been closed, minimized, or taken out of fullscreen from the other monitor.
        Assert.Equal(IntPtr.Zero, FullscreenHideManager.GetWindowToHideFor(Editor, FullscreenHideManager.FrontWindow.Elsewhere, Video, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_AFullscreenAppOnAnotherMonitor_Shows()
    {
        Assert.Equal(IntPtr.Zero, FullscreenHideManager.GetWindowToHideFor(Game, FullscreenHideManager.FrontWindow.Elsewhere, IntPtr.Zero, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_AFullscreenWindowOnTopWithoutTheFocus_HidesForIt()
    {
        // Like a slideshow on a projector while Presenter View has the focus on the laptop's screen, or a video that was already fullscreen there when the widget started.
        Assert.Equal(Slideshow, FullscreenHideManager.GetWindowToHideFor(PresenterView, FullscreenHideManager.FrontWindow.Elsewhere, IntPtr.Zero, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: Slideshow));
        Assert.Equal(Slideshow, FullscreenHideManager.GetWindowToHideFor(Desktop, FullscreenHideManager.FrontWindow.Ignored, IntPtr.Zero, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: Slideshow));
    }

    [Fact]
    public void GetWindowToHideFor_SwitchedToAnotherVirtualDesktop_StaysHiddenWhateversInFront()
    {
        // Showing it would put it on the desktop in use, so it waits to be back on the one with the video, which is its own.
        Assert.Equal(Video, FullscreenHideManager.GetWindowToHideFor(Editor, FullscreenHideManager.FrontWindow.Windowed, Video, FullscreenHideManager.HiddenForWindow.OnAnotherDesktop, fullscreenOnTop: IntPtr.Zero));
        Assert.Equal(Video, FullscreenHideManager.GetWindowToHideFor(Game, FullscreenHideManager.FrontWindow.Fullscreen, Video, FullscreenHideManager.HiddenForWindow.OnAnotherDesktop, fullscreenOnTop: IntPtr.Zero));
        Assert.Equal(Video, FullscreenHideManager.GetWindowToHideFor(Desktop, FullscreenHideManager.FrontWindow.Ignored, Video, FullscreenHideManager.HiddenForWindow.OnAnotherDesktop, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_TheShellInFront_LeavesItAsItWas()
    {
        // Like Alt+Tab over a game, or the desktop, which fills the monitor too.
        Assert.Equal(Game, FullscreenHideManager.GetWindowToHideFor(Desktop, FullscreenHideManager.FrontWindow.Ignored, Game, FullscreenHideManager.HiddenForWindow.StillFills, fullscreenOnTop: IntPtr.Zero));
        Assert.Equal(IntPtr.Zero, FullscreenHideManager.GetWindowToHideFor(Desktop, FullscreenHideManager.FrontWindow.Ignored, IntPtr.Zero, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_TheDesktopAfterWinD_ShowsOnceTheFullscreenAppIsMinimized()
    {
        Assert.Equal(IntPtr.Zero, FullscreenHideManager.GetWindowToHideFor(Desktop, FullscreenHideManager.FrontWindow.Ignored, Video, FullscreenHideManager.HiddenForWindow.Gone, fullscreenOnTop: IntPtr.Zero));
    }

    [Fact]
    public void GetWindowToHideFor_NoWindowInFront_LeavesItAsItWas()
    {
        // Windows says there's none for a moment while one window loses the focus to another.
        Assert.Equal(Video, FullscreenHideManager.GetWindowToHideFor(IntPtr.Zero, FullscreenHideManager.FrontWindow.Ignored, Video, FullscreenHideManager.HiddenForWindow.StillFills, fullscreenOnTop: IntPtr.Zero));
    }
}
