using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class CounterReadingTests
{
    private const long Frequency = 1000;

    [Fact]
    public void GetBytesPerSecond_ShouldDivideByElapsedTime()
    {
        var previous = new CounterReading("a", 1_000, 0);
        var current = new CounterReading("a", 3_000, 2_000);

        Assert.Equal(1_000, CounterReading.GetBytesPerSecond(previous, current, Frequency));
    }

    [Fact]
    public void GetBytesPerSecond_WithoutAnEarlierReading_ReturnsNull()
    {
        var current = new CounterReading("a", 3_000, 2_000);

        Assert.Null(CounterReading.GetBytesPerSecond(default, current, Frequency));
    }

    [Fact]
    public void GetBytesPerSecond_WhenCountersGoBackwards_ReturnsNull()
    {
        var previous = new CounterReading("a", 3_000, 0);
        var current = new CounterReading("a", 1_000, 1_000);

        Assert.Null(CounterReading.GetBytesPerSecond(previous, current, Frequency));
    }

    [Fact]
    public void GetBytesPerSecond_WhenTheAdaptersChange_ReturnsNull()
    {
        var previous = new CounterReading("a", 1_000, 0);
        var current = new CounterReading("a;b", 9_000, 1_000);

        Assert.Null(CounterReading.GetBytesPerSecond(previous, current, Frequency));
    }

    [Fact]
    public void GetBytesPerSecond_WhenNoTimePassed_ReturnsNull()
    {
        var previous = new CounterReading("a", 1_000, 1_000);
        var current = new CounterReading("a", 2_000, 1_000);

        Assert.Null(CounterReading.GetBytesPerSecond(previous, current, Frequency));
    }

    [Fact]
    public void GetBytesPerSecond_ForACounterThatIsStillZero_ReturnsZero()
    {
        var previous = new CounterReading("a", 0, 0);
        var current = new CounterReading("a", 0, 1_000);

        Assert.Equal(0, CounterReading.GetBytesPerSecond(previous, current, Frequency));
    }

    [Fact]
    public void GetBytesPerSecond_WithoutAnyAdapters_ReturnsNull()
    {
        var previous = new CounterReading("", 0, 0);
        var current = new CounterReading("", 0, 1_000);

        Assert.Null(CounterReading.GetBytesPerSecond(previous, current, Frequency));
    }
}
