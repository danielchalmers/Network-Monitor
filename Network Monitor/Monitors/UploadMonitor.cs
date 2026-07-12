using System.Linq;

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
        SetIconColors("#0078D4", "#5CAAE5"); // Fluent blue primary / tint30 https://react.fluentui.dev/?path=/docs/theme-colors--docs
    }

    protected override long GetTotalBytes() =>
         NetworkInterfaces
         .Select(x => x.GetIPStatistics().BytesSent)
         .Sum();
}