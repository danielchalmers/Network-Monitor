using System;
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
    /// How many recent ping outcomes to keep for the hover statistics; roughly the last minute at one ping per second.
    /// </summary>
    private const int MaxSamples = 60;

    private readonly Ping _ping = new();
    private readonly string _host;
    private readonly int _timeout;

    /// <summary>
    /// Recent round trip times in milliseconds, or -1 for a lost ping.
    /// Written from ping completions and read from clock ticks, so access is locked on the queue itself.
    /// </summary>
    private readonly Queue<long> _samples = new();

    private volatile string _lastResult;
    private int _pingInFlight;

    public LatencyMonitor(string host, TimeSpan timeout) : base(true)
    {
        Name = "Latency";
        Icon = '⟳';
        SetIconColors("#F7630C", "#FAA06B"); // Fluent orange primary / tint30 https://react.fluentui.dev/?path=/docs/theme-colors--docs

        _host = host;
        _timeout = (int)timeout.TotalMilliseconds;
    }

    protected override string GetDisplayValue()
    {
        // If the previous ping is still waiting for a reply, the result we show this second is from an older ping.
        IsStale = !TryStartPing();

        return _lastResult ?? NoData;
    }

    protected override string GetDetails()
    {
        long[] samples;

        lock (_samples)
            samples = _samples.ToArray();

        var header = $"{Name} to {_host}";

        if (samples.Length == 0)
            return $"{header}{Environment.NewLine}Waiting for the first reply";

        var last = samples[samples.Length - 1];
        var successes = samples.Where(s => s >= 0).ToArray();
        var losses = samples.Length - successes.Length;

        var lines = new List<string>
        {
            header,
            $"Now: {(last >= 0 ? $"{last} ms" : "Fail")}",
        };

        if (successes.Length > 0)
            lines.Add($"Min/Avg/Max: {successes.Min()} / {successes.Average():0} / {successes.Max()} ms");

        if (successes.Length > 1)
            lines.Add($"Jitter: ±{GetJitter(successes):0} ms");

        lines.Add($"Packet loss: {(double)losses / samples.Length:0%} ({losses} of {samples.Length} pings)");

        if (IsStale)
            lines.Add("Waiting for a reply");

        return string.Join(Environment.NewLine, lines);
    }

    protected override IReadOnlyList<double?> GetHistory()
    {
        lock (_samples)
            return _samples.Select(s => s >= 0 ? (double?)s : null).ToArray();
    }

    /// <summary>
    /// Returns the average difference between consecutive round trip times, which is what makes a connection feel unstable even when the average latency looks fine.
    /// </summary>
    private static double GetJitter(long[] roundtripTimes)
    {
        double totalDifference = 0;

        for (var i = 1; i < roundtripTimes.Length; i++)
            totalDifference += Math.Abs(roundtripTimes[i] - roundtripTimes[i - 1]);

        return totalDifference / (roundtripTimes.Length - 1);
    }

    /// <summary>
    /// Starts a ping in the background and returns whether one was started, or false if the previous one is still waiting for a reply.
    /// Keeps the clock tick from blocking on slow replies, so the timeout setting bounds how long a reply can take without dictating how often the display refreshes.
    /// </summary>
    private bool TryStartPing()
    {
        if (Interlocked.CompareExchange(ref _pingInFlight, 1, 0) != 0)
            return false;

        SendPing();
        return true;
    }

    private async void SendPing()
    {
        try
        {
            var reply = await _ping.SendPingAsync(_host, _timeout).ConfigureAwait(false);
            var success = reply.Status == IPStatus.Success;

            _lastResult = success ? reply.RoundtripTime.ToString() : "Fail";
            RecordSample(success ? reply.RoundtripTime : -1);
        }
        catch
        {
            _lastResult = "Fail";
            RecordSample(-1);
        }
        finally
        {
            Interlocked.Exchange(ref _pingInFlight, 0);
        }
    }

    private void RecordSample(long roundtripTime)
    {
        lock (_samples)
        {
            _samples.Enqueue(roundtripTime);

            while (_samples.Count > MaxSamples)
                _samples.Dequeue();
        }
    }
}
