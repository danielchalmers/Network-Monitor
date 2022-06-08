using System.Diagnostics;
using System.Threading.Tasks;

namespace Network_Monitor.Monitors;

/// <summary>
/// Base for bandwidth type monitors (i.e. upload and download).
/// </summary>
public abstract class BandwidthMonitor : Monitor
{
    private readonly char[] ByteSuffixes = new[] { 'B', 'K', 'M', 'G', 'T', 'P', 'E' };
    private long _lastBytes;

    public override async Task UpdateAsync()
    {
        var bytes = GetBytesDiffAndUpdateLast();

        DisplayValue = Properties.Settings.Default.Bits ?
            GetReadableByteString(bytes * 8).ToLower() :
            GetReadableByteString(bytes);
    }

    protected abstract long GetTotalBytes();

    private long GetBytesDiffAndUpdateLast()
    {
        var bytes = GetTotalBytes();
        try
        {
            // If the last value hasn't been set or is naturally less than zero (interface disconnected, etc), just return 0.
            if (_lastBytes <= 0)
                return 0;

            // Return the difference in bytes since the last call.
            return bytes - _lastBytes;
        }
        finally
        {
            _lastBytes = bytes;
        }
    }

    /// <summary>
    /// Returns a short user-friendly representation of a number of bytes.
    /// </summary>
    private string GetReadableByteString(double bytes)
    {
        if (bytes < 0)
            return "<0B";

        var suffixIndex = 0;
        while (bytes >= 1000) // Keep at 3 or less digits.
        {
            bytes /= 1024;
            suffixIndex++;
        }

        var str = bytes.ToString("0.0");

        if (str.Length > 3)
            str = bytes.ToString("0");

        str += ByteSuffixes[suffixIndex];

        Debug.Assert(str.Length <= 4);

        return str;
    }
}