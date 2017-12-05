using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Monitors.Models;

namespace Network_Monitor.Monitors
{
    /// <summary>
    /// Dummy monitor for preserving auto-generated window width.
    /// Display value is static and contains the max number of characters for any monitor.
    /// </summary>
    public class DummyMonitor : ObservableObject, IMonitor
    {
        public string DisplayValue { get; } = "".PadLeft(5, ' ');

        public MonitorIcon Icon { get; } = new MonitorIcon("X", "You shouldn't see this!", Brushes.Black);

        public async Task UpdateAsync()
        {
            // Don't need to update anything.
        }
    }
}