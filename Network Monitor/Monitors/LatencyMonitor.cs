using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Properties;

namespace Network_Monitor.Monitors;

/// <summary>
/// Monitor for ping latency.
/// </summary>
public class LatencyMonitor : Monitor
{
    private readonly Ping _ping = new();

    public LatencyMonitor()
    {
        Name = "Latency";
        Icon = '⟳';
        IconBrush = Brushes.DarkOrange;
    }

    public override async Task UpdateAsync()
    {
        var reply = await _ping.SendPingAsync(Settings.Default.PingHost, (int)Settings.Default.Timeout.TotalMilliseconds);

        DisplayValue = reply.Status == IPStatus.Success ? reply.RoundtripTime.ToString() : "Fail";
    }
}