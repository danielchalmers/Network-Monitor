using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Network_Monitor.Monitors;

/// <summary>
/// Base for bandwidth type monitors (i.e. upload and download).
/// </summary>
public abstract class BandwidthMonitor : Monitor
{
    private readonly char[] ByteSuffixes = new[] { 'B', 'K', 'M', 'G', 'T', 'P', 'E' };
    private readonly char[] BitSuffixes = new[] { 'b', 'k', 'm', 'g', 't', 'p', 'e' };
    private long _lastBytes;

    protected BandwidthMonitor() : base(TimeSpan.FromSeconds(2))
    {
        NetworkChange.NetworkAvailabilityChanged += (_, _) => NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
    }

    protected IReadOnlyList<NetworkInterface> NetworkInterfaces { get; private set; } = NetworkInterface.GetAllNetworkInterfaces();

    protected abstract long GetTotalBytes();

    protected override string GetDisplayValue()
    {
        var bytes = GetBytesDiffAndUpdateLast();

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
    private string GetReadableByteString(long bytes, bool convertToBits)
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

        return bytes.ToString(bytes < 10 ? "0.0" : "0") + (convertToBits ? BitSuffixes[suffixIndex] : ByteSuffixes[suffixIndex]);
    }
}