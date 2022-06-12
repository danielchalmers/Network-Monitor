using System;
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

    protected BandwidthMonitor() : base(TimeSpan.FromSeconds(2))
    {
    }

    protected abstract long GetTotalBytes();

    protected override async Task<string> GetDisplayValueAsync()
    {
        var bytes = await Task.Run(() => GetBytesDiffAndUpdateLast());

        if (!bytes.HasValue)
            return string.Empty;

        return GetReadableByteString(bytes.Value, Properties.Settings.Default.Bits);
    }

    private long? GetBytesDiffAndUpdateLast()
    {
        var bytes = GetTotalBytes();
        try
        {
            // Last value hasn't been set or an interface was disconnected.
            if (_lastBytes <= 0 || bytes <= 0)
                return null;

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
    private string GetReadableByteString(double bytes, bool convertToBits)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Number of bytes must be positive for this.");

        if (convertToBits)
            bytes *= 8;

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

        if (convertToBits)
            str = str.ToLower();

        Debug.Assert(str.Length <= 4);

        return str;
    }
}