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
        SetIconColors("#107C10", "#54B054"); // Fluent green primary / tint30 https://react.fluentui.dev/?path=/docs/theme-colors--docs
    }

    protected override long GetTotalBytes() =>
         NetworkInterfaces
         .Select(x => x.GetIPStatistics().BytesReceived)
         .Sum();
}