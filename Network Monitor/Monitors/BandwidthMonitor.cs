using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace Network_Monitor.Monitors;

/// <summary>
/// Base for bandwidth type monitors (i.e. upload and download).
/// </summary>
public abstract class BandwidthMonitor : Monitor
{
    /// <summary>
    /// How many recent rates to keep for the hover statistics; roughly the last minute at one sample per second.
    /// </summary>
    private const int MaxSamples = 60;

    private readonly char[] ByteSuffixes = new[] { 'B', 'K', 'M', 'G', 'T', 'P', 'E' };
    private readonly char[] BitSuffixes = new[] { 'b', 'k', 'm', 'g', 't', 'p', 'e' };
    private static readonly string[] ByteRateUnits = new[] { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
    private static readonly string[] BitRateUnits = new[] { "bps", "kbps", "Mbps", "Gbps", "Tbps" };
    private static readonly string[] SizeUnits = new[] { "B", "KB", "MB", "GB", "TB" };

    /// <summary>
    /// Recent rates in bytes per second.
    /// Only touched from clock ticks, so no locking is needed.
    /// </summary>
    private readonly Queue<double> _samples = new();

    private long _lastBytes;
    private long _lastTimestamp;
    private long _sessionBytes;

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
            return NoData;

        _samples.Enqueue(bytesPerSecond.Value);

        while (_samples.Count > MaxSamples)
            _samples.Dequeue();

        return GetReadableByteString(bytesPerSecond.Value, Properties.Settings.Default.Bits);
    }

    protected override string GetDetails()
    {
        var lines = new List<string> { Name };

        if (_samples.Count > 0)
        {
            var now = _samples.Last();
            var bits = Properties.Settings.Default.Bits;

            // Both units on the primary line so the "Measure in bits" setting is never a commitment.
            lines.Add($"Now: {FormatRate(now, asBits: false)} ({FormatRate(now, asBits: true)})");
            lines.Add($"Avg / Peak: {FormatRate(_samples.Average(), bits)} / {FormatRate(_samples.Max(), bits)}");
        }

        lines.Add($"This session: {FormatWithUnits(_sessionBytes, SizeUnits)}");

        return string.Join(Environment.NewLine, lines);
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

        _sessionBytes += bytes - lastBytes;

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

    /// <summary>
    /// Returns a transfer rate with full unit names for the hover details, where space isn't at a premium.
    /// </summary>
    private static string FormatRate(double bytesPerSecond, bool asBits) =>
        FormatWithUnits(asBits ? bytesPerSecond * 8 : bytesPerSecond, asBits ? BitRateUnits : ByteRateUnits);

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
