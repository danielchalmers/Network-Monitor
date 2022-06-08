using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

/// <summary>
/// Monitor for upload bandwidth usage.
/// </summary>
public class UploadMonitor : BandwidthMonitor
{
    public UploadMonitor()
    {
        Name = "Upload";
        Icon = '↑';
        IconBrush = Brushes.Blue;
    }

    protected override long GetTotalBytes() =>
         NetworkInterface
         .GetAllNetworkInterfaces()
         .Select(x => x.GetIPStatistics().BytesSent)
         .Sum();
}