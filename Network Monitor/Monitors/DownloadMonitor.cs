using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Utils;

namespace Network_Monitor.Monitors
{
    public class DownloadMonitor : ObservableObject, IMonitor
    {
        private string _displayValue;
        private long _lastValue = NetworkUtil.GetTotalBytesDownloaded();

        public string DisplayValue
        {
            get => _displayValue;
            set => Set(ref _displayValue, value);
        }

        public string Icon { get; } = "↓";
        public SolidColorBrush IconColor { get; } = Brushes.Green;

        public async Task<string> GetNewDisplayValueAsync()
        {
            var value = GetCurrentValue();

            return ByteUtil.BytesToReadableString(value);
        }

        private long GetCurrentValue()
        {
            var totalDownloadedBytes = NetworkUtil.GetTotalBytesDownloaded();
            try
            {
                // Return bytes since last check.
                return totalDownloadedBytes - _lastValue;
            }
            finally
            {
                _lastValue = totalDownloadedBytes;
            }
        }
    }
}