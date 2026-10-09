using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

[UseInvariantCulture]
public class RateFormatterTests
{
    [Theory]
    [InlineData(1_200_000, true, "9.6m")]
    [InlineData(1_200_000, false, "1.2M")]
    [InlineData(85_000, false, "85K")]
    [InlineData(500, true, "4.0k")]
    [InlineData(12, false, "12B")]
    public void FormatCompactRate_ShouldUseShortSuffixes(double bytesPerSecond, bool asBits, string expected)
    {
        Assert.Equal(expected, RateFormatter.FormatCompactRate(bytesPerSecond, asBits));
    }

    [Fact]
    public void FormatCompactRate_Zero_ShowsOneDecimal()
    {
        Assert.Equal("0.0b", RateFormatter.FormatCompactRate(0, asBits: true));
    }

    [Theory]
    [InlineData(999_600, false, "1000K")]
    [InlineData(1_248_000, true, "10.0m")]
    public void FormatCompactRate_JustBelowAUnit_RoundsUpToFiveCharacters(double bytesPerSecond, bool asBits, string expected)
    {
        Assert.Equal(expected, RateFormatter.FormatCompactRate(bytesPerSecond, asBits));
    }

    [Theory]
    [InlineData(11_000, false, "11 KB/s")]
    [InlineData(11_000, true, "88 kbps")]
    [InlineData(5_600_000, true, "45 Mbps")]
    [InlineData(0, true, "0.0 bps")]
    public void FormatRate_ShouldUseFullUnitNames(double bytesPerSecond, bool asBits, string expected)
    {
        Assert.Equal(expected, RateFormatter.FormatRate(bytesPerSecond, asBits));
    }

    [Theory]
    [InlineData(28_000_000, "28 MB")]
    [InlineData(2_500_000, "2.5 MB")]
    [InlineData(4_000_000_000_000_000, "4000 TB")]
    public void FormatSize_ShouldUseFullUnitNames(double bytes, string expected)
    {
        Assert.Equal(expected, RateFormatter.FormatSize(bytes));
    }
}
