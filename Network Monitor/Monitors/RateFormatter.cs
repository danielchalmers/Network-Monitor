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
    /// The result is never more than four characters, so the reading doesn't shrink to fit as traffic changes.
    /// </summary>
    public static string FormatCompactRate(double bytesPerSecond, bool asBits)
    {
        var value = asBits ? bytesPerSecond * 8 : bytesPerSecond;

        // An idle connection reads as a plain zero rather than "0.0b".
        if (value < 0.05)
            return "0";

        var suffixes = asBits ? BitSuffixes : ByteSuffixes;
        var suffixIndex = 0;

        // Step up a unit just before a value would round up to 1000, so 999.6 K shows as 1.0M rather than 1000K.
        while (value >= 999.5 && suffixIndex < suffixes.Length - 1)
        {
            value /= 1000;
            suffixIndex++;
        }

        return FormatNumber(value) + suffixes[suffixIndex];
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
        if (value < 0.05)
            return "0 " + units[0];

        var unitIndex = 0;

        while (value >= 999.5 && unitIndex < units.Length - 1)
        {
            value /= 1000;
            unitIndex++;
        }

        return FormatNumber(value) + " " + units[unitIndex];
    }

    /// <summary>
    /// Returns one decimal place below 10 and none above, deciding by the rounded value so 9.96 shows as "10" rather than "10.0".
    /// </summary>
    private static string FormatNumber(double value) => value.ToString(value < 9.95 ? "0.0" : "0");
}
