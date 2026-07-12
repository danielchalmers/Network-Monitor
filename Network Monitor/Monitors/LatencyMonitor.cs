using System;
using System.Net.NetworkInformation;

namespace Network_Monitor.Monitors;

/// <summary>
/// Monitor for ping latency.
/// </summary>
public class LatencyMonitor : Monitor
{
    private readonly Ping _ping = new();
    private readonly string _host;
    private readonly int _timeout;

    public LatencyMonitor(string host, TimeSpan timeout) : base(timeout)
    {
        Name = "Latency";
        Icon = '⟳';
        SetIconColors("#F7630C", "#FAA06B"); // Fluent orange primary / tint30 https://react.fluentui.dev/?path=/docs/theme-colors--docs

        _host = host;
        _timeout = (int)timeout.TotalMilliseconds;
    }

    protected override string GetDisplayValue()
    {
        var reply = _ping.Send(_host, _timeout);

        return reply.Status == IPStatus.Success ? reply.RoundtripTime.ToString() : "Fail";
    }
}