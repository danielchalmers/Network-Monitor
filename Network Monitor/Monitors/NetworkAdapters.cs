using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace Network_Monitor.Monitors;

/// <summary>
/// Helpers for choosing which network adapters to monitor.
/// </summary>
public static class NetworkAdapters
{
    /// <summary>
    /// Returns the real, monitorable adapters: connected, non-loopback, and carrying an IP address.
    /// Requiring an IP address excludes the WFP/QoS filter-driver pseudo-interfaces that mirror a parent adapter's byte counters; summing those alongside the parent would count the same traffic several times over.
    /// </summary>
    public static IReadOnlyList<NetworkInterface> GetMonitorable() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsMonitorable)
            .ToArray();

    private static bool IsMonitorable(NetworkInterface adapter)
    {
        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            return false;

        if (adapter.OperationalStatus != OperationalStatus.Up)
            return false;

        try
        {
            return adapter.GetIPProperties().UnicastAddresses.Count > 0;
        }
        catch
        {
            // Some adapter types don't support IP properties; treat those as not monitorable rather than letting the whole enumeration fail.
            return false;
        }
    }
}
