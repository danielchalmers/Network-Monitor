using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Media;
using Network_Monitor.Utils;

namespace Network_Monitor.Monitors
{
    public class UploadMonitor : ObservableObject, IMonitor
    {
        private string _displayValue;
        private long _lastBytes;

        public string DisplayValue
        {
            get => _displayValue;
            private set => Set(ref _displayValue, value);
        }

        public string Icon { get; } = "↑";
        public SolidColorBrush IconColor { get; } = Brushes.Blue;
        public string IconToolTip { get; } = "Upload";

        public async Task<string> GetNewDisplayValueAsync()
        {
            var bytes = GetBytesDifferenceAndSetLast();

            return Properties.Settings.Default.Bits
                ? ByteUtil.BytesToReadableString(ByteUtil.GetBits(bytes)).ToLower()
                : ByteUtil.BytesToReadableString(bytes);
        }

        public async Task UpdateDisplayValueAsync() => DisplayValue = await GetNewDisplayValueAsync();

        private long GetBytesDifferenceAndSetLast()
        {
            var bytes = GetTotalBytesUploaded();
            try
            {
                // If the last value hasn't been set or is naturally less than zero (due to interface disconnect, etc), just return 0.
                if (_lastBytes <= 0)
                {
                    return 0;
                }

                // Return the difference in bytes since the last call.
                return bytes - _lastBytes;
            }
            finally
            {
                _lastBytes = bytes;
            }
        }

        private long GetTotalBytesUploaded() =>
             NetworkInterface
             .GetAllNetworkInterfaces()
             .Select(x => x.GetIPStatistics().BytesSent)
             .Sum();
    }
}