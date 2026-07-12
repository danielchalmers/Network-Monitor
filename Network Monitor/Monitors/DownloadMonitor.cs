using System.Linq;

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
        IconBrush = CreateIconBrush("#00E676"); // Green A400 https://materialui.co/colors
    }

    protected override long GetTotalBytes() =>
         NetworkInterfaces
         .Select(x => x.GetIPStatistics().BytesReceived)
         .Sum();
}