using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Network_Monitor.Monitors;

/// <summary>
/// Base for bandwidth type monitors (i.e. upload and download).
/// </summary>
public abstract class BandwidthMonitor : Monitor
{
    private readonly char[] ByteSuffixes = new[] { 'B', 'K', 'M', 'G', 'T', 'P', 'E' };
    private readonly char[] BitSuffixes = new[] { 'b', 'k', 'm', 'g', 't', 'p', 'e' };
    private long _lastBytes;
    private long _lastTimestamp;

    protected BandwidthMonitor() : base(true)
    {
        NetworkChange.NetworkAvailabilityChanged += (_, _) => NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
    }

    protected IReadOnlyList<NetworkInterface> NetworkInterfaces { get; private set; } = NetworkInterface.GetAllNetworkInterfaces();

    protected abstract long GetTotalBytes();

    protected override string GetDisplayValue()
    {
        var bytesPerSecond = GetBytesPerSecondAndUpdateLast();

        if (!bytesPerSecond.HasValue)
            return string.Empty;

        return GetReadableByteString(bytesPerSecond.Value, Properties.Settings.Default.Bits);
    }

    /// <summary>
    /// Returns the transfer rate since the last call, normalized by the actual elapsed time so timer jitter doesn't skew the reading.
    /// </summary>
    private double? GetBytesPerSecondAndUpdateLast()
    {
        var bytes = GetTotalBytes();
        var timestamp = Stopwatch.GetTimestamp();
        var lastBytes = _lastBytes;
        var lastTimestamp = _lastTimestamp;

        _lastBytes = bytes;
        _lastTimestamp = timestamp;

        // Last value hasn't been set, or an interface was disconnected and its counters reset.
        if (lastBytes <= 0 || bytes < lastBytes)
            return null;

        var elapsedSeconds = (timestamp - lastTimestamp) / (double)Stopwatch.Frequency;

        if (elapsedSeconds <= 0)
            return null;

        return (bytes - lastBytes) / elapsedSeconds;
    }

    /// <summary>
    /// Returns a short user-friendly representation of a transfer rate in bytes per second.
    /// </summary>
    private string GetReadableByteString(double bytes, bool convertToBits)
    {
        if (convertToBits)
            bytes *= 8;

        var suffixIndex = 0;
        while (bytes >= 1000) // Keep at 3 or less digits.
        {
            bytes /= 1000;
            suffixIndex++;
        }

        return bytes.ToString(bytes < 10 ? "0.0" : "0") + (convertToBits ? BitSuffixes[suffixIndex] : ByteSuffixes[suffixIndex]);
    }
}
