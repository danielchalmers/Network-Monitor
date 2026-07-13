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

    /// <summary>
    /// Guards the cached adapter list and the delta baseline, which the clock tick reads while network-change events rewrite the list.
    /// </summary>
    private readonly object _measureLock = new();
    private IReadOnlyList<NetworkInterface> _monitorableInterfaces = NetworkAdapters.GetMonitorable();
    private string _lastBasis;
    private long _lastBytes;
    private long _lastTimestamp;
    private long _sessionBytes;

    protected BandwidthMonitor() : base(true)
    {
        // Address changes fire on adapter connect/disconnect too, unlike availability which only fires when the machine gains or loses networking entirely.
        NetworkChange.NetworkAvailabilityChanged += (_, _) => RefreshInterfaces();
        NetworkChange.NetworkAddressChanged += (_, _) => RefreshInterfaces();

        // Rebuild the cache when the selection changes so a freshly picked adapter is found even if its network-change event was delayed or missed.
        Properties.Settings.Default.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.InterfaceId))
                RefreshInterfaces();
        };
    }

    /// <summary>
    /// Sums the byte counters of the given interfaces (received for download, sent for upload).
    /// </summary>
    protected abstract long GetTotalBytes(IReadOnlyList<NetworkInterface> interfaces);

    /// <summary>
    /// The interfaces to count traffic on: the adapter picked in the context menu, or all monitorable adapters.
    /// Must be called under <see cref="_measureLock" /> so the resolved set stays consistent with the baseline measured against it.
    /// </summary>
    private IReadOnlyList<NetworkInterface> GetSelectedInterfaces()
    {
        var interfaces = _monitorableInterfaces;
        var interfaceId = Properties.Settings.Default.InterfaceId;

        return string.IsNullOrEmpty(interfaceId)
            ? interfaces
            : interfaces.Where(x => x.Id == interfaceId).ToArray();
    }

    /// <summary>
    /// Rebuilds the cached adapter list so newly connected or removed adapters are picked up.
    /// The baseline isn't touched here; a changed adapter set is detected during measurement instead, which keeps the set and its baseline atomic.
    /// </summary>
    private void RefreshInterfaces()
    {
        var refreshed = NetworkAdapters.GetMonitorable();

        lock (_measureLock)
            _monitorableInterfaces = refreshed;
    }

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

        var interfaceId = Properties.Settings.Default.InterfaceId;

        if (!string.IsNullOrEmpty(interfaceId))
            lines.Add($"Adapter: {_monitorableInterfaces.FirstOrDefault(x => x.Id == interfaceId)?.Name ?? "Disconnected"}");

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
        // Resolving the adapter set, reading its counters, and updating the baseline all happen under one lock so a delta can never pair new counters with a stale baseline.
        lock (_measureLock)
        {
            var interfaces = GetSelectedInterfaces();
            var basis = GetBasis(interfaces);
            var bytes = GetTotalBytes(interfaces);
            var timestamp = Stopwatch.GetTimestamp();

            var lastBasis = _lastBasis;
            var lastBytes = _lastBytes;
            var lastTimestamp = _lastTimestamp;

            _lastBasis = basis;
            _lastBytes = bytes;
            _lastTimestamp = timestamp;

            // Warm up (no rate this tick) when there's no prior reading, the counters dropped because an interface went away, or the adapter set changed since the last reading.
            if (lastBytes <= 0 || bytes < lastBytes || basis != lastBasis)
                return null;

            var elapsedSeconds = (timestamp - lastTimestamp) / (double)Stopwatch.Frequency;

            if (elapsedSeconds <= 0)
                return null;

            _sessionBytes += bytes - lastBytes;

            return (bytes - lastBytes) / elapsedSeconds;
        }
    }

    /// <summary>
    /// Returns an order-independent signature of the interface set, so a delta is only computed when this tick measured the same adapters as the last one.
    /// </summary>
    private static string GetBasis(IReadOnlyList<NetworkInterface> interfaces) =>
        string.Join(";", interfaces.Select(x => x.Id).OrderBy(id => id));

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
