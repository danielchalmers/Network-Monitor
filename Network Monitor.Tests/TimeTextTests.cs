using System;

namespace Network_Monitor.Tests;

[UseInvariantCulture]
public class TimeTextTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 14, 2, 0);

    [Fact]
    public void FormatMoment_Today_ShowsTheTime()
    {
        Assert.Equal("08:14", TimeText.FormatMoment(new DateTime(2026, 10, 7, 8, 14, 0), Now));
    }

    [Fact]
    public void FormatMoment_EarlierThisWeek_ShowsTheWeekdayAndTime()
    {
        Assert.Equal("Mon 08:14", TimeText.FormatMoment(new DateTime(2026, 10, 5, 8, 14, 0), Now));
    }

    [Fact]
    public void FormatMoment_LongerAgo_ShowsTheDate()
    {
        Assert.Equal("09/20/2026", TimeText.FormatMoment(new DateTime(2026, 9, 20, 8, 14, 0), Now));
    }
}
