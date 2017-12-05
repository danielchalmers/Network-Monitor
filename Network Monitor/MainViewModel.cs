using System.Collections.Generic;
using System.Threading.Tasks;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            Monitors = new List<IMonitor> {
                new LatencyMonitor(),
                new DownloadMonitor(),
                new UploadMonitor()
            };
        }

        public List<IMonitor> Monitors { get; }

        public async Task StartMonitoring()
        {
            while (true)
            {
                foreach (var monitor in Monitors)
                {
                    try
                    {
                        await monitor.UpdateAsync();
                    }
                    catch
                    {
                    }
                }
                await Task.Delay(Settings.Default.Interval);
            }
        }
    }
}