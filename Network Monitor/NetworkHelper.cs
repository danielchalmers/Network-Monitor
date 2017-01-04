using System.Linq;
using System.Net.NetworkInformation;
using Network_Monitor.Properties;

namespace Network_Monitor
{
    public static class NetworkHelper
    {
        public static PingReply GetLatency(this Ping ping)
        {
            return string.IsNullOrWhiteSpace(Settings.Default.PingHost)
                ? null
                : ping.Send(Settings.Default.PingHost, 5000);
        }

        public static long GetDownloadedBytes()
            => NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPStatistics().BytesReceived).Sum();

        public static long GetUploadedBytes()
            => NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPStatistics().BytesSent).Sum();
    }
}