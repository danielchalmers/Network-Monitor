using System.Linq;
using System.Net.NetworkInformation;
using Network_Monitor.Properties;

namespace Network_Monitor
{
    public static class NetworkHelper
    {
        public static PingReply GetLatency()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.PingHost))
            {
                return null;
            }
            var ping = new Ping();
            var reply = ping.Send(Settings.Default.PingHost, 5000);
            return reply;
        }

        public static long GetDownloadedBytes()
            => NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPStatistics().BytesReceived).Sum();

        public static long GetUploadedBytes()
            => NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPStatistics().BytesSent).Sum();
    }
}