using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Media;
using Network_Monitor.Monitors.Models;

namespace Network_Monitor.Monitors
{
    /// <summary>
    /// Monitor for upload bandwidth usage.
    /// </summary>
    public class UploadMonitor : BandwidthMonitorBase
    {
        public UploadMonitor()
        {
            Icon = new MonitorIcon("↑", "Upload", Brushes.Blue);
        }

        protected override long GetTotalBytes() =>
             NetworkInterface
             .GetAllNetworkInterfaces()
             .Select(x => x.GetIPStatistics().BytesSent)
             .Sum();
    }
}