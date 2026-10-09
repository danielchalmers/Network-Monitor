namespace Network_Monitor.Tests;

public class SystemClockTimerTests
{
    [Theory]
    [InlineData(2, 998)]
    [InlineData(500, 500)]
    [InlineData(800, 200)]
    [InlineData(801, 1199)]
    [InlineData(990, 1010)]
    public void GetMillisecondsUntilTick_SkipsABoundaryThatsOnlyMomentsAway(int currentMillisecond, int expected)
    {
        Assert.Equal(expected, SystemClockTimer.GetMillisecondsUntilTick(currentMillisecond));
    }
}
