using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Media;

namespace Network_Monitor.Monitors
{
    /// <summary>
    /// Monitor for download bandwidth usage.
    /// </summary>
    public class DownloadMonitor : BandwidthMonitorBase
    {
        public DownloadMonitor()
        {
            Icon = new MonitorIcon("↓", "Download", Brushes.Green);
        }

        protected override long GetTotalBytes() =>
             NetworkInterface
             .GetAllNetworkInterfaces()
             .Select(x => x.GetIPStatistics().BytesReceived)
             .Sum();
    }
}