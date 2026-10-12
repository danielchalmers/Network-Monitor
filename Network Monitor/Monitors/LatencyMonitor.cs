using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

    /// <summary>
    /// Pings the router while the internet isn't answering, to tell whether an outage is on your own network or past it.
    /// </summary>
    private readonly RouterCheck _routerCheck;

    private int _pingsOut;

    /// <summary>
    /// The address being pinged, as it was typed, which the tooltip shows.
    /// </summary>
    private string _host;

    /// <summary>
    /// The address being pinged.
    /// It and <see cref="_host" /> are only touched from the clock tick, which moves them to the one picked in the menu.
    /// </summary>
    private IPAddress _address;

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

    /// <summary>
    /// What the latest tick found out about the router, said along with the outage clock, or null.
    /// </summary>
    private (string Note, string Spoken)? _routerNote;

    public LatencyMonitor(TimeSpan timeout) : base(true)
    {
        Name = "Latency";
        Icon = '⟳';
        SetIconColors("#F7630C", "#FAA06B"); // Fluent orange primary / tint30 https://react.fluentui.dev/?path=/docs/theme-colors--docs

        // A healthy connection's few milliseconds of wobble should look like a wobble, not fill the graph's whole height like a real spike.
        HistoryMinimumTop = 50;

        _timeout = (int)timeout.TotalMilliseconds;
        _maxPingsOut = (int)Math.Ceiling(timeout.TotalSeconds) + 2;
        _routerCheck = new RouterCheck(timeout);
    }

    protected override string GetDisplayValue()
    {
        FollowPingHost();

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
            NetworkStatus.HasRepliedOnThisNetwork(_address));

        StartPing();
        RememberOutage(view, snapshot.LastReplyAt, now);

        _routerNote = CheckRouter(view, snapshot.SecondsSinceReply);
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

        if (_routerNote is var (routerNote, _))
            lines.Add(routerNote);

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
    /// Moves to the address picked in the menu, from this tick on.
    /// The last minute's stats, graph, and outage were measured against the old address, so they start fresh rather than being shown under the new one, as they do when another adapter is picked.
    /// So, as after the PC wakes, there's no outage clock until the new address has replied once, even if it's one that replied before.
    /// This is checked here on the clock tick, which owns the history, rather than when the setting changes.
    /// </summary>
    private void FollowPingHost()
    {
        var host = Properties.Settings.Default.PingHost;

        // Most ticks it hasn't changed. The settings only keep an address that can be pinged, so it can always be read.
        if (host == _host || !PingAddress.TryParse(host, out var address))
            return;

        // The first tick has nothing to start over from, and the same address written another way, like in capitals, only changes how it's shown.
        if (_address != null && !address.Equals(_address))
        {
            _history.Reset();
            _outageStart = null;
            _lastOutage = null;
        }

        _host = host;
        _address = address;
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

    /// <summary>
    /// Pings the router while replies from the internet are missing, and returns what it showed once the outage clock is up.
    /// </summary>
    /// <param name="view">What the reading shows this tick.</param>
    /// <param name="secondsSinceReply">Whole seconds since the newest second whose ping got a reply, as of the snapshot the view came from.</param>
    private (string Note, string Spoken)? CheckRouter(LatencyView view, long? secondsSinceReply)
    {
        if (!RouterCheck.IsNeeded(view, secondsSinceReply))
        {
            _routerCheck.Stop();
            return null;
        }

        _routerCheck.Check(NetworkStatus.Routers, _address);

        var (reply, hasAnswered) = _routerCheck.GetState();
        return RouterCheck.Describe(reply, hasAnswered, view.IsOutage);
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

        SendPing(ticket, _address);
    }

    /// <summary>
    /// Sends one ping and files how it went.
    /// The address is passed in rather than read when the reply comes back, so a reply that arrives after another address was picked counts for the one it came from.
    /// </summary>
    private async void SendPing(LatencyHistory.Ticket ticket, IPAddress address)
    {
        if (!_idlePings.TryTake(out var ping))
            ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(address, _timeout).ConfigureAwait(false);
            var success = reply.Status == IPStatus.Success;

            if (success)
                NetworkStatus.ReportReply(address);

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

    protected override string GetSpokenValue(string displayValue)
    {
        if (displayValue != _view.Value)
            return base.GetSpokenValue(displayValue);

        return _routerNote is var (_, routerSpoken) ? $"{Name}, {_view.Spoken}, {routerSpoken}" : $"{Name}, {_view.Spoken}";
    }

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
