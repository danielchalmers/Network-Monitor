using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Utils;

namespace Network_Monitor.Monitors
{
    public class UploadMonitor : ObservableObject, IMonitor
    {
        private string _displayValue;
        private long _lastValue = NetworkUtil.GetTotalBytesUploaded();

        public string DisplayValue
        {
            get => _displayValue;
            set => Set(ref _displayValue, value);
        }

        public string Icon { get; } = "↑";
        public SolidColorBrush IconColor { get; } = Brushes.Blue;

        public async Task<string> GetNewDisplayValueAsync()
        {
            var value = GetCurrentValue();

            if (Properties.Settings.Default.Bits)
            {
                value *= 8;
            }

            return ByteUtil.BytesToReadableString(value);
        }

        private long GetCurrentValue()
        {
            var totalUploadedBytes = NetworkUtil.GetTotalBytesUploaded();
            try
            {
                // Return bytes since last check.
                return totalUploadedBytes - _lastValue;
            }
            finally
            {
                _lastValue = totalUploadedBytes;
            }
        }
    }
}