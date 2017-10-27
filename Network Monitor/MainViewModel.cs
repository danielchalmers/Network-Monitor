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
                try
                {
                    foreach (var monitor in Monitors)
                    {
                        monitor.DisplayValue = await monitor.GetNewDisplayValueAsync();
                    }
                }
                catch
                {
                }
                finally
                {
                    await Task.Delay(Settings.Default.Interval);
                }
            }
        }
    }
}