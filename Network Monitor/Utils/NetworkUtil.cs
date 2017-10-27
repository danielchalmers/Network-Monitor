using System.Linq;
using System.Net.NetworkInformation;

namespace Network_Monitor.Utils
{
    public static class NetworkUtil
    {
        public static long GetTotalBytesDownloaded() =>
            NetworkInterface
            .GetAllNetworkInterfaces()
            .Select(x => x.GetIPStatistics().BytesReceived)
            .Sum();

        public static long GetTotalBytesUploaded() =>
            NetworkInterface
            .GetAllNetworkInterfaces()
            .Select(x => x.GetIPStatistics().BytesSent)
            .Sum();
    }
}