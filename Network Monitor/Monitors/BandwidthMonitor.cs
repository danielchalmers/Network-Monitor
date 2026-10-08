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
    private IReadOnlyList<NetworkInterface> _monitorableInterfaces = Array.Empty<NetworkInterface>();
    private NetworkInterface _internetAdapter;
    private (int Index, System.Net.Sockets.AddressFamily Family)? _internetRoute;

    /// <summary>
    /// The adapter Automatic last measured, kept through moments without a route so a dropout doesn't count as moving to another adapter.
    /// </summary>
    private string _lastInternetAdapterId;

    /// <summary>
    /// Ticks left before looking for the internet adapter again while there's a route but its adapter wasn't listed yet.
    /// </summary>
    private int _ticksUntilRetry;

    private CounterReading _lastReading;
    private long _sessionBytes;
    private DateTime _sessionStart = DateTime.Now;
    private string _lastSelection;
    private bool _skipNextSample;

    protected BandwidthMonitor() : base(true)
    {
        RefreshInterfaces();

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
    /// The interfaces to count traffic on: the adapter picked in the context menu, the one that reaches the internet (Automatic), or all of them.
    /// Must be called under <see cref="_measureLock" /> so the resolved set stays consistent with the baseline measured against it.
    /// </summary>
    private IReadOnlyList<NetworkInterface> GetSelectedInterfaces() =>
        NetworkAdapters.Select(_monitorableInterfaces, Properties.Settings.Default.InterfaceId, _internetAdapter);

    /// <summary>
    /// Rebuilds the cached adapter list so newly connected or removed adapters are picked up, and finds which one reaches the internet now.
    /// The baseline isn't touched here; a changed adapter set is detected during measurement instead, which keeps the set and its baseline atomic.
    /// </summary>
    private void RefreshInterfaces()
    {
        var refreshed = NetworkAdapters.GetMonitorable();
        var route = NetworkAdapters.GetInternetRoute();
        var internetAdapter = NetworkAdapters.FindInternetAdapter(refreshed);

        lock (_measureLock)
        {
            _monitorableInterfaces = refreshed;
            _internetRoute = route;
            _internetAdapter = internetAdapter;

            if (internetAdapter != null)
                _lastInternetAdapterId = internetAdapter.Id;
        }
    }

    /// <summary>
    /// Checks whether the route to the internet has moved to another adapter, which can happen without a network-change event, like when a VPN adds its routes after connecting or one connection's priority drops below another's.
    /// Called on the clock tick while Automatic is picked. The adapters are only listed again when the route has moved.
    /// </summary>
    private void FollowInternetRoute()
    {
        var route = NetworkAdapters.GetInternetRoute();
        bool shouldRefresh;

        lock (_measureLock)
        {
            // A brand new route's adapter can take a moment to be listed, so look again every few seconds until it is.
            var isUnlisted = route is not null && _internetAdapter is null && --_ticksUntilRetry <= 0;
            shouldRefresh = !Equals(route, _internetRoute) || isUnlisted;

            if (shouldRefresh)
                _ticksUntilRetry = 5;
        }

        if (shouldRefresh)
            RefreshInterfaces();
    }

    /// <summary>
    /// Returns what's being measured, which changes when another adapter is picked or when Automatic moves to a different one, like after plugging in a cable.
    /// Losing the route for a moment, like when Wi-Fi drops, isn't a change, so the session total carries on when it comes back.
    /// </summary>
    private string GetSelectionKey()
    {
        var interfaceId = Properties.Settings.Default.InterfaceId;

        lock (_measureLock)
            return string.IsNullOrEmpty(interfaceId) ? "automatic:" + _lastInternetAdapterId : interfaceId;
    }

    protected override string GetDisplayValue()
    {
        if (string.IsNullOrEmpty(Properties.Settings.Default.InterfaceId))
            FollowInternetRoute();

        // Stats from the previously measured adapter would otherwise be shown under the new one's name.
        // This is checked here on the clock tick, which owns the samples, rather than when the setting changes.
        var selection = GetSelectionKey();

        if (selection != _lastSelection)
        {
            _samples.Clear();
            _sessionBytes = 0;
            _sessionStart = DateTime.Now;
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

    protected override string GetSpokenValue(string displayValue) =>
        displayValue == NoData || _samples.Count == 0
            ? base.GetSpokenValue(displayValue)
            : $"{Name}, {RateFormatter.FormatSpokenRate(_samples.Last(), Properties.Settings.Default.Bits)}";

    protected override void ResetHistory()
    {
        _samples.Clear();
        _skipNextSample = true;
    }

    protected override string GetDetails()
    {
        var lines = new List<string> { Name };

        lock (_measureLock)
            lines.Add(NetworkAdapters.Describe(_monitorableInterfaces, Properties.Settings.Default.InterfaceId, _internetAdapter));

        if (_samples.Count > 0)
        {
            var now = _samples.Last();
            var bits = Properties.Settings.Default.Bits;

            // Both units on the primary line, the chosen one first, so the "Measure in bits" setting is never a commitment.
            lines.Add($"Now: {RateFormatter.FormatRate(now, bits)} ({RateFormatter.FormatRate(now, !bits)})");
            lines.Add($"Avg / Peak: {RateFormatter.FormatRate(_samples.Average(), bits)} / {RateFormatter.FormatRate(_samples.Max(), bits)}");
        }

        lines.Add($"Total: {RateFormatter.FormatSize(_sessionBytes)} since {TimeText.FormatMoment(_sessionStart, DateTime.Now)}");

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
