using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

namespace Network_Monitor.Monitors;

/// <summary>
/// Monitor for ping latency.
/// </summary>
public class LatencyMonitor : Monitor
{
    /// <summary>
    /// How many seconds of pings the hover statistics cover.
    /// </summary>
    private const int WindowSeconds = 60;

    private readonly string _host;
    private readonly int _timeout;

    /// <summary>
    /// How many pings can be waiting for a reply at once: one a second for as long as a reply is waited for, and a little spare.
    /// </summary>
    private readonly int _maxPingsOut;

    private readonly LatencyHistory _history = new(WindowSeconds);

    /// <summary>
    /// Ping objects not in use. A Ping can only send one request at a time, and a ping is sent every second even while earlier ones are still waiting for a reply.
    /// </summary>
    private readonly ConcurrentBag<Ping> _idlePings = new();

    private int _pingsOut;

    /// <summary>
    /// What the reading showed on the latest tick, which the tooltip and screen readers describe.
    /// </summary>
    private LatencyView _view = LatencyView.Note("Waiting for the first reply", "waiting for the first reply");

    /// <summary>
    /// When the reply before the current outage arrived, in UTC, while the outage clock is showing.
    /// </summary>
    private DateTime? _outageStart;

    /// <summary>
    /// The latest outage that ended, in UTC, for the tooltip.
    /// </summary>
    private (DateTime Start, DateTime End)? _lastOutage;

    public LatencyMonitor(string host, TimeSpan timeout) : base(true)
    {
        Name = "Latency";
        Icon = '⟳';
        SetIconColors("#F7630C", "#FAA06B"); // Fluent orange primary / tint30 https://react.fluentui.dev/?path=/docs/theme-colors--docs

        // A healthy connection's few milliseconds of wobble should look like a wobble, not fill the graph's whole height like a real spike.
        HistoryMinimumTop = 50;

        _host = host;
        _timeout = (int)timeout.TotalMilliseconds;
        _maxPingsOut = (int)Math.Ceiling(timeout.TotalSeconds) + 2;
    }

    protected override string GetDisplayValue()
    {
        // Show the last second whose ping has finished, then send this second's.
        var snapshot = _history.GetSnapshot();
        var finished = LatencyHistory.GetFinished(snapshot.Slots);
        var now = DateTime.UtcNow;

        NetworkStatus.FollowInternetRoute();

        var view = Outage.Decide(
            finished.Length > 0 ? finished[finished.Length - 1] : null,
            GetReading(snapshot.Slots).IsWaiting,
            finished.Where(s => s >= 0).Select(s => (long?)s).LastOrDefault(),
            snapshot.SecondsSinceReply,
            snapshot.LastReplyAt,
            now,
            NetworkStatus.IsReconnecting,
            NetworkStatus.HasRepliedOnThisNetwork);

        StartPing();
        RememberOutage(view, snapshot.LastReplyAt, now);

        _view = view;
        IsStale = view.IsStale;
        return view.Value;
    }

    protected override bool HasLiveReading => _view.IsLive;

    protected override string GetDetails()
    {
        var finished = LatencyHistory.GetFinished(_history.GetSnapshot().Slots);
        var successes = finished.Where(s => s >= 0).ToArray();
        var losses = finished.Length - successes.Length;
        var view = _view;

        var lines = new List<string> { $"{Name} to {_host}" };

        if ((view.IsLive || view.IsStale) && successes.Length > 0)
            lines.Add($"Now: {FormatRoundtrip(successes[successes.Length - 1])} ms");

        if (view.NoteLine is not null)
            lines.Add(view.NoteLine);

        if (successes.Length > 0)
            lines.Add($"Min/Avg/Max: {FormatRoundtrip(successes.Min())} / {FormatRoundtrip((long)Math.Round(successes.Average(), MidpointRounding.AwayFromZero))} / {FormatRoundtrip(successes.Max())} ms");

        if (successes.Length > 1)
            lines.Add($"Jitter: {LatencyMath.GetJitter(successes):0} ms");

        // One ping a second, so each finished ping is a second.
        if (finished.Length > 0)
            lines.Add($"Packet loss: {(double)losses / finished.Length:0%} of the last {finished.Length} s");

        var now = DateTime.UtcNow;

        if (_lastOutage is var (start, end) && now - end < Outage.RecoveryShownFor)
            lines.Add(Outage.DescribeRecovery(start, end, now));

        return string.Join(Environment.NewLine, lines);
    }

    protected override void ResetHistory()
    {
        _history.Reset();

        // Time asleep isn't an outage.
        _outageStart = null;
    }

    /// <summary>
    /// Notes when an outage clock starts, and once replies are back, how long the outage lasted.
    /// </summary>
    /// <param name="view">What the reading shows this tick.</param>
    /// <param name="lastReplyAt">When the last reply arrived, in UTC, as of the snapshot the view came from.</param>
    /// <param name="now">The current time, in UTC.</param>
    private void RememberOutage(LatencyView view, DateTime? lastReplyAt, DateTime now)
    {
        if (view.IsOutage)
        {
            _outageStart ??= lastReplyAt;
        }
        else if (view.IsBlockedNetwork)
        {
            // Time on a network that blocks ping isn't an outage.
            _outageStart = null;
        }
        else if (view.IsLive && _outageStart is DateTime start)
        {
            _lastOutage = (start, lastReplyAt ?? now);
            _outageStart = null;
        }
    }

    protected override IReadOnlyList<double?> GetHistory() =>
        LatencyHistory.GetFinished(_history.GetSnapshot().Slots).Select(s => s >= 0 ? (double?)s : null).ToArray();

    /// <summary>
    /// Returns what to show from <paramref name="slots" /> (oldest first): the newest finished second's round trip time, or null if it got no reply or there's none yet, and whether a newer ping is still waiting for its reply.
    /// </summary>
    public static (long? Roundtrip, bool IsWaiting) GetReading(IReadOnlyList<long> slots)
    {
        for (var i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] != LatencyHistory.Pending)
                return (slots[i] >= 0 ? slots[i] : null, i < slots.Count - 1);
        }

        return (null, slots.Count > 0);
    }

    /// <summary>
    /// Sends this second's ping in the background, filed under this second however long the reply takes.
    /// Keeps the clock tick from blocking on slow replies, and keeps one ping a second however slow the replies are, so packet loss is counted over time rather than over however many pings happened to be sent.
    /// </summary>
    private void StartPing()
    {
        var ticket = _history.Open();

        // Should replies stop coming back without ever timing out, don't pile up pings; that second counts as lost.
        if (Interlocked.Increment(ref _pingsOut) > _maxPingsOut)
        {
            Interlocked.Decrement(ref _pingsOut);
            _history.Complete(ticket, null);
            return;
        }

        SendPing(ticket);
    }

    private async void SendPing(LatencyHistory.Ticket ticket)
    {
        if (!_idlePings.TryTake(out var ping))
            ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(_host, _timeout).ConfigureAwait(false);
            var success = reply.Status == IPStatus.Success;

            if (success)
                NetworkStatus.ReportReply();

            _history.Complete(ticket, success ? reply.RoundtripTime : null);
            _idlePings.Add(ping);
        }
        catch
        {
            // No route or no network at all; a Ping that failed this way isn't reused.
            _history.Complete(ticket, null);
            ping.Dispose();
        }
        finally
        {
            Interlocked.Decrement(ref _pingsOut);
        }
    }

    protected override string GetSpokenValue(string displayValue) =>
        displayValue == _view.Value ? $"{Name}, {_view.Spoken}" : base.GetSpokenValue(displayValue);

    /// <summary>
    /// Returns a latency reading as a screen reader should say it.
    /// </summary>
    public static string FormatSpoken(string displayValue) => displayValue switch
    {
        NoData => "no reading",
        "<1" => "less than 1 millisecond",
        "1" => "1 millisecond",
        _ when long.TryParse(displayValue, out _) => $"{displayValue} milliseconds",
        _ => displayValue,
    };

    /// <summary>
    /// Returns a round trip time in milliseconds, or "&lt;1" for a reply that came back within a millisecond, which ping reports as zero.
    /// </summary>
    public static string FormatRoundtrip(long roundtripTime) => roundtripTime > 0 ? roundtripTime.ToString() : "<1";
}
