namespace Network_Monitor.Monitors;

/// <summary>
/// Turns transfer rates and byte counts into text.
/// </summary>
public static class RateFormatter
{
    private static readonly char[] ByteSuffixes = new[] { 'B', 'K', 'M', 'G', 'T', 'P', 'E' };
    private static readonly char[] BitSuffixes = new[] { 'b', 'k', 'm', 'g', 't', 'p', 'e' };
    private static readonly string[] ByteRateUnits = new[] { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
    private static readonly string[] BitRateUnits = new[] { "bps", "kbps", "Mbps", "Gbps", "Tbps" };
    private static readonly string[] SizeUnits = new[] { "B", "KB", "MB", "GB", "TB" };

    /// <summary>
    /// Returns a short representation of a transfer rate for the widget, where every character counts.
    /// </summary>
    public static string FormatCompactRate(double bytesPerSecond, bool asBits)
    {
        var value = asBits ? bytesPerSecond * 8 : bytesPerSecond;

        var suffixIndex = 0;
        while (value >= 1000) // Keep at 3 or less digits.
        {
            value /= 1000;
            suffixIndex++;
        }

        return value.ToString(value < 10 ? "0.0" : "0") + (asBits ? BitSuffixes[suffixIndex] : ByteSuffixes[suffixIndex]);
    }

    /// <summary>
    /// Returns a transfer rate with full unit names for the hover details, where space isn't at a premium.
    /// </summary>
    public static string FormatRate(double bytesPerSecond, bool asBits) =>
        FormatWithUnits(asBits ? bytesPerSecond * 8 : bytesPerSecond, asBits ? BitRateUnits : ByteRateUnits);

    /// <summary>
    /// Returns an amount of data, such as a session total, with full unit names.
    /// </summary>
    public static string FormatSize(double bytes) => FormatWithUnits(bytes, SizeUnits);

    private static string FormatWithUnits(double value, string[] units)
    {
        var unitIndex = 0;

        while (value >= 1000 && unitIndex < units.Length - 1)
        {
            value /= 1000;
            unitIndex++;
        }

        return value.ToString(value < 10 ? "0.0" : "0") + " " + units[unitIndex];
    }
}
