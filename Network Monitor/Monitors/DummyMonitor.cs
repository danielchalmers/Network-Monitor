using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors
{
    /// <summary>
    /// Dummy monitor for preserving auto-generated window width.
    /// </summary>
    public class DummyMonitor : ObservableObject, IMonitor
    {
        public string DisplayValue { get; } = "".PadLeft(4, ' ');

        public MonitorIcon Icon { get; } = new MonitorIcon("X", "You shouldn't see this!", Brushes.Black);

        public async Task UpdateAsync()
        {
            // Don't need to update anything.
        }
    }
}