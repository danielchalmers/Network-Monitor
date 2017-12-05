using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Monitors.Models;
using Network_Monitor.Properties;

namespace Network_Monitor.Monitors
{
    /// <summary>
    /// Monitor for ping latency.
    /// </summary>
    public class LatencyMonitor : ObservableObject, IMonitor
    {
        private readonly Ping _ping = new Ping();
        private string _displayValue;

        public string DisplayValue
        {
            get => _displayValue;
            private set => Set(ref _displayValue, value);
        }

        public MonitorIcon Icon { get; } = new MonitorIcon("⟳", "Latency", Brushes.DarkOrange);

        public async Task UpdateAsync()
        {
            async Task<string> GetUpdatedValue()
            {
                var reply = await _ping.SendPingAsync(Settings.Default.PingHost, (int)Settings.Default.Timeout.TotalMilliseconds);
                var latency = reply.RoundtripTime;
                var status = reply.Status;

                return status == IPStatus.Success ? latency.ToString() : "Error";
            }
            DisplayValue = await GetUpdatedValue();
        }
    }
}