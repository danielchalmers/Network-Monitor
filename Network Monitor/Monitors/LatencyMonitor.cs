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
        IconBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9100")); // Orange A400 https://materialui.co/colors
    }

    protected override async Task<string> GetDisplayValueAsync()
    {
        var reply = await _ping.SendPingAsync(Settings.Default.PingHost, (int)Settings.Default.Timeout.TotalMilliseconds);

        return reply.Status == IPStatus.Success ? reply.RoundtripTime.ToString() : "Fail";
    }
}