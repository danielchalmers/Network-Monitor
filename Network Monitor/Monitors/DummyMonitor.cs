using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

/// <summary>
/// Dummy monitor for preserving auto-generated window width.
/// </summary>
public class DummyMonitor : Monitor
{
    public DummyMonitor()
    {
        Name = "You shouldn't be seeing this!";
        Icon = 'X';
        IconBrush = Brushes.Red;

        DisplayValue = "".PadLeft(4, ' ');
    }

    public override Task UpdateAsync() =>
        Task.CompletedTask; // Don't need to update anything.
}