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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FormatCompactRate_Zero_IsAPlainZero(bool asBits)
    {
        Assert.Equal("0", RateFormatter.FormatCompactRate(0, asBits));
    }

    [Theory]
    [InlineData(999_600, false, "1.0M")]
    [InlineData(1_248_000, true, "10m")]
    [InlineData(124_900, true, "999k")]
    [InlineData(124_950, true, "1.0m")]
    public void FormatCompactRate_JustBelowAUnit_StaysWithinFourCharacters(double bytesPerSecond, bool asBits, string expected)
    {
        Assert.Equal(expected, RateFormatter.FormatCompactRate(bytesPerSecond, asBits));
    }

    [Fact]
    public void FormatCompactRate_IsNeverLongerThanFourCharacters()
    {
        for (var value = 0.001; value < 1e18; value *= 1.0007)
        {
            Assert.True(RateFormatter.FormatCompactRate(value, asBits: false).Length <= 4, $"{value} B/s");
            Assert.True(RateFormatter.FormatCompactRate(value, asBits: true).Length <= 4, $"{value} B/s in bits");
        }
    }

    [Theory]
    [InlineData(11_000, false, "11 KB/s")]
    [InlineData(11_000, true, "88 kbps")]
    [InlineData(5_600_000, true, "45 Mbps")]
    [InlineData(0, true, "0 bps")]
    [InlineData(1_244_000, false, "1.2 MB/s")]
    [InlineData(999_600, false, "1.0 MB/s")]
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
