namespace Network_Monitor.Monitors;

/// <summary>
/// One reading of the byte counter summed over a set of adapters.
/// </summary>
public readonly struct CounterReading
{
    public CounterReading(string basis, long bytes, long timestamp)
    {
        Basis = basis;
        Bytes = bytes;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Order-independent signature of the adapters that were summed, or null when there's no reading yet.
    /// </summary>
    public string Basis { get; }

    /// <summary>
    /// Total bytes counted across the adapters.
    /// </summary>
    public long Bytes { get; }

    /// <summary>
    /// When the reading was taken, in <see cref="System.Diagnostics.Stopwatch" /> ticks.
    /// </summary>
    public long Timestamp { get; }

    /// <summary>
    /// Returns the transfer rate between two readings in bytes per second, or null when they can't be compared.
    /// Readings can't be compared when there's no earlier one, no adapters were measured, the counters went backwards because an adapter went away, a different set of adapters was measured, or no time passed.
    /// The rate is normalized by the actual time between the readings so timer jitter doesn't skew it.
    /// </summary>
    public static double? GetBytesPerSecond(CounterReading previous, CounterReading current, long timestampFrequency)
    {
        if (previous.Basis is null || string.IsNullOrEmpty(current.Basis) || current.Bytes < previous.Bytes || current.Basis != previous.Basis)
            return null;

        var elapsedSeconds = (current.Timestamp - previous.Timestamp) / (double)timestampFrequency;

        if (elapsedSeconds <= 0)
            return null;

        return (current.Bytes - previous.Bytes) / elapsedSeconds;
    }
}
