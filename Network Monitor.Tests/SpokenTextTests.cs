using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

[UseInvariantCulture]
public class SpokenTextTests
{
    [Theory]
    [InlineData(5_600_000, true, "45 megabits per second")]
    [InlineData(5_600_000, false, "5.6 megabytes per second")]
    [InlineData(0, true, "0 bits per second")]
    public void FormatSpokenRate_SpellsOutTheUnit(double bytesPerSecond, bool asBits, string expected)
    {
        Assert.Equal(expected, RateFormatter.FormatSpokenRate(bytesPerSecond, asBits));
    }

    [Theory]
    [InlineData("22", "22 milliseconds")]
    [InlineData("1", "1 millisecond")]
    [InlineData("<1", "less than 1 millisecond")]
    [InlineData("—", "no reading")]
    public void FormatSpoken_ReadsLatencyAsWords(string displayValue, string expected)
    {
        Assert.Equal(expected, LatencyMonitor.FormatSpoken(displayValue));
    }
}
