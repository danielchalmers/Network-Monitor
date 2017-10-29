using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Properties;

namespace Network_Monitor.Monitors
{
    public class LatencyMonitor : ObservableObject, IMonitor
    {
        private readonly Ping _ping = new Ping();
        private string _displayValue;

        public string DisplayValue
        {
            get => _displayValue;
            set => Set(ref _displayValue, value);
        }

        public string Icon { get; } = "⟳";
        public SolidColorBrush IconColor { get; } = Brushes.DarkOrange;
        public string IconToolTip { get; } = "Latency";

        public async Task<string> GetNewDisplayValueAsync()
        {
            var reply = await _ping.SendPingAsync(Settings.Default.PingHost, (int)Settings.Default.Timeout.TotalMilliseconds);
            var latency = reply.RoundtripTime;
            var status = reply.Status;

            return status == IPStatus.Success ? latency.ToString() : "Error";
        }
    }
}