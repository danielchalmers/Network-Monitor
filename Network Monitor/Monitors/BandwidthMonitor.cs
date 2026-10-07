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
    private CounterReading _lastReading;
    private long _sessionBytes;
    private string _lastSelection;
    private bool _skipNextSample;

    protected BandwidthMonitor() : base(true)
    {
        // A throughput graph reads as magnitude, so its scale is anchored at zero; min–max would blow idle-traffic wiggles up into dramatic-looking noise.
        HistoryStartsAtZero = true;

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
        // Stats from the previously picked adapter would otherwise be shown under the new one's name.
        // This is checked here on the clock tick, which owns the samples, rather than when the setting changes.
        var selection = Properties.Settings.Default.InterfaceId;

        if (selection != _lastSelection)
        {
            _samples.Clear();
            _sessionBytes = 0;
            _lastSelection = selection;
        }

        var bytesPerSecond = GetBytesPerSecondAndUpdateLast();

        // The first rate after the clock stopped is averaged over the whole gap, so it still counts toward the total but isn't shown as a reading.
        if (_skipNextSample)
        {
            _skipNextSample = false;
            return NoData;
        }

        if (!bytesPerSecond.HasValue)
            return NoData;

        _samples.Enqueue(bytesPerSecond.Value);

        while (_samples.Count > MaxSamples)
            _samples.Dequeue();

        return RateFormatter.FormatCompactRate(bytesPerSecond.Value, Properties.Settings.Default.Bits);
    }

    protected override IReadOnlyList<double?> GetHistory() =>
        _samples.Select(s => (double?)s).ToArray();

    protected override void ResetHistory()
    {
        _samples.Clear();
        _skipNextSample = true;
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
            lines.Add($"Now: {RateFormatter.FormatRate(now, asBits: false)} ({RateFormatter.FormatRate(now, asBits: true)})");
            lines.Add($"Avg / Peak: {RateFormatter.FormatRate(_samples.Average(), bits)} / {RateFormatter.FormatRate(_samples.Max(), bits)}");
        }

        lines.Add($"This session: {RateFormatter.FormatSize(_sessionBytes)}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Returns the transfer rate since the last call, or null when the readings can't be compared (see <see cref="CounterReading.GetBytesPerSecond" />).
    /// </summary>
    private double? GetBytesPerSecondAndUpdateLast()
    {
        // Resolving the adapter set, reading its counters, and updating the baseline all happen under one lock so a delta can never pair new counters with a stale baseline.
        lock (_measureLock)
        {
            var interfaces = GetSelectedInterfaces();
            var reading = new CounterReading(GetBasis(interfaces), GetTotalBytes(interfaces), Stopwatch.GetTimestamp());
            var previous = _lastReading;
            _lastReading = reading;

            var bytesPerSecond = CounterReading.GetBytesPerSecond(previous, reading, Stopwatch.Frequency);

            if (bytesPerSecond.HasValue)
                _sessionBytes += reading.Bytes - previous.Bytes;

            return bytesPerSecond;
        }
    }

    /// <summary>
    /// Returns an order-independent signature of the interface set, so a delta is only computed when this tick measured the same adapters as the last one.
    /// </summary>
    private static string GetBasis(IReadOnlyList<NetworkInterface> interfaces) =>
        string.Join(";", interfaces.Select(x => x.Id).OrderBy(id => id));
}
