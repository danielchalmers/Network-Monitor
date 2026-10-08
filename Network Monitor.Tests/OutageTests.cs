using System;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

[UseInvariantCulture]
public class OutageTests
{
    private const long Lost = LatencyHistory.Lost;
    private static readonly DateTime Now = new(2026, 10, 7, 14, 4, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(3, "0:03")]
    [InlineData(59, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(599, "9:59")]
    [InlineData(600, "0h10")]
    [InlineData(660, "0h11")]
    [InlineData(35_999, "9h59")]
    [InlineData(36_000, "10h")]
    [InlineData(360_000, "100h")]
    [InlineData(36_000_000, "999h")]
    public void FormatClock_CountsUpInFourCharacters(int seconds, string expected)
    {
        Assert.Equal(expected, Outage.FormatClock(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatClock_NeverNeedsMoreThanFourCharacters()
    {
        for (long seconds = 0; seconds < 10_000_000; seconds = (seconds * 11 / 10) + 1)
            Assert.InRange(Outage.FormatClock(TimeSpan.FromSeconds(seconds)).Length, 1, 4);
    }

    [Theory]
    [InlineData(42, "42 s")]
    [InlineData(102, "1:42")]
    public void FormatShort_UsesSecondsUnderAMinute(int seconds, string expected)
    {
        Assert.Equal(expected, Outage.FormatShort(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(1, "1 second")]
    [InlineData(42, "42 seconds")]
    [InlineData(102, "1 minute 42 seconds")]
    [InlineData(120, "2 minutes")]
    [InlineData(3_660, "1 hour 1 minute")]
    public void FormatSpoken_SpellsOutTheUnits(int seconds, string expected)
    {
        Assert.Equal(expected, Outage.FormatSpoken(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void DescribeRecovery_SaysHowLongAndWhen()
    {
        var start = new DateTime(2026, 10, 7, 14, 2, 0, DateTimeKind.Utc);

        Assert.Equal($"Back after 1:42 ({Local(start)}–{Local(start.AddSeconds(102))})", Outage.DescribeRecovery(start, start.AddSeconds(102), Now));
        Assert.Equal($"Back after 12 s ({Local(start)})", Outage.DescribeRecovery(start, start.AddSeconds(12), Now));
    }

    [Fact]
    public void Decide_WhenRepliesArrive_ShowsTheNumber()
    {
        var view = Decide(20, secondsSinceReply: 1);

        Assert.Equal("20", view.Value);
        Assert.True(view.IsLive);
        Assert.Null(view.NoteLine);
        Assert.Equal("20 milliseconds", view.Spoken);
    }

    [Fact]
    public void Decide_OnALinkSlowerThanASecond_StaysLive()
    {
        // With 1.2 s round trips there's always a newer ping out, but a reply still arrives every second.
        var view = Decide(1200, isWaiting: true, secondsSinceReply: 2, lastReplyAgo: 0.8);

        Assert.True(view.IsLive);
        Assert.False(view.IsStale);
    }

    [Fact]
    public void Decide_ForTheFirstSecondOrTwoWithoutAReply_DimsTheLastNumber()
    {
        var view = Decide(Lost, secondsSinceReply: 2);

        Assert.Equal("20", view.Value);
        Assert.True(view.IsStale);
        Assert.Equal("Waiting for a reply", view.NoteLine);
    }

    [Fact]
    public void Decide_WhileASlowReplyIsOut_DimsTheLastNumber()
    {
        var view = Decide(20, isWaiting: true, secondsSinceReply: 2);

        Assert.Equal("20", view.Value);
        Assert.True(view.IsStale);
    }

    [Fact]
    public void Decide_AfterThreeSecondsWithoutAReply_ShowsAClock()
    {
        var view = Decide(Lost, secondsSinceReply: 3);

        Assert.Equal("0:03", view.Value);
        Assert.False(view.IsStale);
        Assert.True(view.IsOutage);
        Assert.Equal($"No reply for 3 s (since {Local(Now.AddSeconds(-2.98))})", view.NoteLine);
        Assert.Equal("no reply for 3 seconds", view.Spoken);
    }

    [Fact]
    public void Decide_CountsTheClockInWholeTicks()
    {
        // Counted in clock ticks rather than from when the reply arrived, so it never shows a second twice or skips one.
        for (var seconds = 3; seconds < 700; seconds++)
            Assert.Equal(Outage.FormatClock(TimeSpan.FromSeconds(seconds)), Decide(Lost, secondsSinceReply: seconds).Value);
    }

    [Fact]
    public void Decide_WhileSlowPingsAreStillOut_CountsFromTheLastReply()
    {
        // Pings into a dead connection take seconds to time out, but the clock counts from the last reply, not the first failure.
        Assert.Equal("0:04", Decide(20, isWaiting: true, secondsSinceReply: 4).Value);
    }

    [Fact]
    public void Decide_AfterElevenMinutes_ShowsHoursAndMinutes()
    {
        Assert.Equal("0h11", Decide(Lost, secondsSinceReply: 660).Value);
    }

    [Fact]
    public void Decide_OnANetworkThatHasNeverReplied_SaysItMayBlockPing()
    {
        var view = Decide(Lost, secondsSinceReply: null, hasRepliedOnThisNetwork: false);

        Assert.Equal("—", view.Value);
        Assert.False(view.IsOutage);
        Assert.True(view.IsBlockedNetwork);
        Assert.Equal("No replies on this network yet. It may block ping.", view.NoteLine);
    }

    [Fact]
    public void Decide_AfterMovingToANetworkThatHasNeverReplied_SaysItMayBlockPing()
    {
        Assert.True(Decide(Lost, secondsSinceReply: 30, hasRepliedOnThisNetwork: false).IsBlockedNetwork);
    }

    [Fact]
    public void Decide_BeforeTheFirstPingFinishes_WaitsRatherThanBlamingTheNetwork()
    {
        var view = Decide(null, isWaiting: true, secondsSinceReply: null, hasRepliedOnThisNetwork: false);

        Assert.Equal("—", view.Value);
        Assert.Equal("Waiting for the first reply", view.NoteLine);
    }

    [Fact]
    public void Decide_RightAfterReconnecting_SaysSoInsteadOfAClock()
    {
        var view = Decide(Lost, secondsSinceReply: 40, isReconnecting: true);

        Assert.Equal("—", view.Value);
        Assert.Equal("Reconnecting…", view.NoteLine);
        Assert.False(view.IsOutage);
    }

    [Fact]
    public void Decide_AfterWakingWithoutAReplyYet_DoesntCountAnOutage()
    {
        var view = Decide(Lost, secondsSinceReply: null);

        Assert.Equal("—", view.Value);
        Assert.False(view.IsOutage);
        Assert.Equal("Waiting for a reply", view.NoteLine);
    }

    [Fact]
    public void GetSnapshot_CountsSecondsFromTheLastSecondThatGotAReply()
    {
        var history = new LatencyHistory(60);
        var replied = history.Open();
        history.Complete(replied, 20, Now);
        history.Complete(history.Open(), null, Now);
        history.Complete(history.Open(), null, Now);

        // A late reply to an older ping doesn't wind the count back.
        history.Complete(new LatencyHistory.Ticket(replied.Sequence - 1, replied.Generation), 20, Now);

        var snapshot = history.GetSnapshot();
        Assert.Equal(3, snapshot.SecondsSinceReply);
        Assert.Equal(Now, snapshot.LastReplyAt);
    }

    [Fact]
    public void GetSnapshot_AfterAReset_ForgetsTheLastReply()
    {
        var history = new LatencyHistory(60);
        history.Complete(history.Open(), 20, Now);
        var beforeSleep = history.Open();

        history.Reset();
        history.Complete(beforeSleep, 20, Now);

        var snapshot = history.GetSnapshot();
        Assert.Null(snapshot.SecondsSinceReply);
        Assert.Null(snapshot.LastReplyAt);
    }

    private static string Local(DateTime utc) => TimeText.FormatMoment(utc.ToLocalTime(), Now.ToLocalTime());

    /// <param name="secondsSinceReply">Clock ticks since the second whose ping last got a reply.</param>
    /// <param name="lastReplyAgo">How long ago that reply arrived; by default, just after its ping was sent.</param>
    private static LatencyView Decide(long? newestFinished, bool isWaiting = false, int? secondsSinceReply = 1, bool isReconnecting = false, bool hasRepliedOnThisNetwork = true, double? lastReplyAgo = null) =>
        Outage.Decide(
            newestFinished,
            isWaiting,
            20,
            secondsSinceReply,
            secondsSinceReply is int seconds ? Now.AddSeconds(-(lastReplyAgo ?? seconds - 0.02)) : null,
            Now,
            isReconnecting,
            hasRepliedOnThisNetwork);
}
