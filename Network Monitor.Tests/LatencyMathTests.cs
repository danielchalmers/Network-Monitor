using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class LatencyMathTests
{
    [Fact]
    public void GetJitter_ShouldAverageTheDifferenceBetweenConsecutivePings()
    {
        Assert.Equal(10, LatencyMath.GetJitter(new long[] { 20, 30, 20, 30 }));
    }

    [Fact]
    public void GetJitter_ForASteadyConnection_IsZero()
    {
        Assert.Equal(0, LatencyMath.GetJitter(new long[] { 20, 20, 20 }));
    }

    [Fact]
    public void GetJitter_WithFewerThanTwoPings_IsZero()
    {
        Assert.Equal(0, LatencyMath.GetJitter(new long[] { 20 }));
    }

    [Theory]
    [InlineData(0, "<1")]
    [InlineData(1, "1")]
    [InlineData(23, "23")]
    public void FormatRoundtrip_ShowsUnderAMillisecondAsLessThanOne(long roundtripTime, string expected)
    {
        Assert.Equal(expected, LatencyMonitor.FormatRoundtrip(roundtripTime));
    }
}
