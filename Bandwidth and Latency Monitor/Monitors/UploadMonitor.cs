using System.Linq;
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
        IconBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2979FF")); // Blue A400 https://materialui.co/colors
    }

    protected override long GetTotalBytes() =>
         NetworkInterfaces
         .Select(x => x.GetIPStatistics().BytesSent)
         .Sum();
}