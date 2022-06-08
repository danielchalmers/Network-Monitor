using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

/// <summary>
/// Monitor for download bandwidth usage.
/// </summary>
public class DownloadMonitor : BandwidthMonitor
{
    public DownloadMonitor()
    {
        Name = "Download";
        Icon = '↓';
        IconBrush = Brushes.Green;
    }

    protected override long GetTotalBytes() =>
         NetworkInterface
         .GetAllNetworkInterfaces()
         .Select(x => x.GetIPStatistics().BytesReceived)
         .Sum();
}