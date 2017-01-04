using System.Linq;
using System.Net.NetworkInformation;
using Network_Monitor.Properties;

namespace Network_Monitor
{
    public static class NetworkHelper
    {
        public static PingReply GetLatency(this Ping ping)
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.PingHost))
            {
                return null;
            }
            try
            {
                return ping.Send(Settings.Default.PingHost, 4000);
            }
            catch (PingException)
            {
                return null;
            }
        }

        public static long GetDownloadedBytes()
            => NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPStatistics().BytesReceived).Sum();

        public static long GetUploadedBytes()
            => NetworkInterface.GetAllNetworkInterfaces().Select(x => x.GetIPStatistics().BytesSent).Sum();
    }
}