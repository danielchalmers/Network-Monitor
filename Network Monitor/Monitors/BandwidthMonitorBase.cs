using System;
using System.Threading.Tasks;
using Network_Monitor.Monitors.Models;
using Network_Monitor.Utils;

namespace Network_Monitor.Monitors
{
    /// <summary>
    /// Base class for bandwidth type monitors (i.e. upload and download).
    /// </summary>
    public abstract class BandwidthMonitorBase : ObservableObject, IMonitor
    {
        private string _displayValue;
        private long _lastBytes;

        public string DisplayValue
        {
            get => _displayValue;
            private set => Set(ref _displayValue, value);
        }

        public MonitorIcon Icon { get; protected set; }

        public async Task UpdateAsync()
        {
            string GetUpdatedValue()
            {
                var bytes = GetBytesDiffAndUpdateLast();

                return Properties.Settings.Default.Bits
                    ? GetReadableByteString(ByteUtil.GetBits(bytes)).ToLower()
                    : GetReadableByteString(bytes);
            }
            DisplayValue = GetUpdatedValue();
        }

        protected virtual long GetTotalBytes()
        {
            throw new NotImplementedException();
        }

        private long GetBytesDiffAndUpdateLast()
        {
            var bytes = GetTotalBytes();
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

        /// <summary>
        /// Return a user-friendly representation of a number of bytes.
        /// </summary>
        private string GetReadableByteString(long bytes)
        {
            if (bytes < 0)
            {
                return "<0B";
            }
            var readableBytes = ByteUtil.GetReadableBytes(bytes, out var suffix);
            var readableBytesString = readableBytes.ToString("0.0");
            var cappedReadableBytesString = readableBytesString.Length > 3
                ? readableBytes.ToString("0")
                : readableBytesString;
            return cappedReadableBytesString + suffix;
        }
    }
}