using System;
using System.Net.NetworkInformation;
using System.Threading;

namespace Network_Monitor.Monitors;

/// <summary>
/// Monitor for ping latency.
/// </summary>
public class LatencyMonitor : Monitor
{
    private readonly Ping _ping = new();
    private readonly string _host;
    private readonly int _timeout;
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

            _lastResult = reply.Status == IPStatus.Success ? reply.RoundtripTime.ToString() : "Fail";
        }
        catch
        {
            _lastResult = "Fail";
        }
        finally
        {
            Interlocked.Exchange(ref _pingInFlight, 0);
        }
    }
}
