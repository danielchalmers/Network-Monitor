using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Media;

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
        IconBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9100")); // Orange A400 https://materialui.co/colors

        _host = host;
        _timeout = (int)timeout.TotalMilliseconds;
    }

    protected override async Task<string> GetDisplayValueAsync()
    {
        var reply = await _ping.SendPingAsync(_host, _timeout);

        return reply.Status == IPStatus.Success ? reply.RoundtripTime.ToString() : "Fail";
    }
}