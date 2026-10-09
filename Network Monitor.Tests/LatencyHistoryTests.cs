using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class LatencyHistoryTests
{
    private const long Pending = LatencyHistory.Pending;
    private const long Lost = LatencyHistory.Lost;

    [Fact]
    public void Complete_FilesEachReplyUnderTheSecondItWasSent()
    {
        var history = new LatencyHistory(60);
        var first = history.Open();
        var second = history.Open();

        // The second ping comes back before the slow first one.
        history.Complete(second, 20);
        Assert.Equal(new[] { Pending, 20 }, history.GetSlots());

        history.Complete(first, 900);
        Assert.Equal(new long[] { 900, 20 }, history.GetSlots());
    }

    [Fact]
    public void Complete_WithoutAReply_CountsAsLost()
    {
        var history = new LatencyHistory(60);
        history.Complete(history.Open(), null);

        Assert.Equal(new[] { Lost }, history.GetSlots());
    }

    [Fact]
    public void Open_KeepsTheWindowPlusTheSecondStillOut()
    {
        var history = new LatencyHistory(3);

        for (var i = 1; i <= 5; i++)
            history.Complete(history.Open(), i);

        Assert.Equal(new long[] { 2, 3, 4, 5 }, history.GetSlots());
    }

    [Fact]
    public void Complete_AfterItsSecondLeftTheWindow_IsIgnored()
    {
        var history = new LatencyHistory(1);
        var old = history.Open();
        history.Open();
        history.Open();

        history.Complete(old, 20);

        Assert.Equal(new[] { Pending, Pending }, history.GetSlots());
    }

    [Fact]
    public void Complete_ForAPingSentBeforeAReset_IsIgnored()
    {
        // A ping sent just before the PC slept can come back after it wakes, and doesn't belong to the new minute.
        var history = new LatencyHistory(60);
        var beforeSleep = history.Open();
        history.Reset();
        var afterWake = history.Open();

        history.Complete(beforeSleep, 20);
        Assert.Equal(new[] { Pending }, history.GetSlots());

        history.Complete(afterWake, 30);
        Assert.Equal(new long[] { 30 }, history.GetSlots());
    }

    [Fact]
    public void GetFinished_LeavesOutPingsStillOut()
    {
        Assert.Equal(new[] { 20, Lost }, LatencyHistory.GetFinished(new[] { 20, Pending, Lost, Pending }));
    }

    [Fact]
    public void GetReading_ShowsTheNewestFinishedSecond()
    {
        Assert.Equal((20, false), LatencyMonitor.GetReading(new long[] { 30, 20 }));
    }

    [Fact]
    public void GetReading_WhileAReplyIsSlow_KeepsThePreviousReadingAndSaysItsWaiting()
    {
        Assert.Equal((20, true), LatencyMonitor.GetReading(new[] { 20, Pending }));
    }

    [Fact]
    public void GetReading_AfterALostPing_HasNoReading()
    {
        Assert.Equal((null, false), LatencyMonitor.GetReading(new[] { 20, Lost }));
    }

    [Fact]
    public void GetReading_BeforeAnyReply_HasNoReading()
    {
        Assert.Equal((null, false), LatencyMonitor.GetReading(new long[0]));
        Assert.Equal((null, true), LatencyMonitor.GetReading(new[] { Pending }));
    }
}
