using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Network_Monitor.Monitors;

/// <summary>
/// Helpers for choosing which network adapters to monitor.
/// </summary>
public static class NetworkAdapters
{
    /// <summary>
    /// The adapter setting that measures every adapter combined.
    /// An empty setting means Automatic, which measures whichever adapter reaches the internet.
    /// </summary>
    public const string AllAdapters = "*";

    /// <summary>
    /// Public addresses (Google DNS) used only to ask Windows which adapter its route to the internet leaves through.
    /// Nothing is sent to them.
    /// </summary>
    private static readonly IPAddress[] InternetAddresses = { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:4860:4860::8888") };

    /// <summary>
    /// Returns the real, monitorable adapters: connected, non-loopback, and carrying an IP address.
    /// Requiring an IP address excludes the WFP/QoS filter-driver pseudo-interfaces that mirror a parent adapter's byte counters; summing those alongside the parent would count the same traffic several times over.
    /// </summary>
    public static IReadOnlyList<NetworkInterface> GetMonitorable() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsMonitorable)
            .ToArray();

    /// <summary>
    /// Returns the adapter among <paramref name="adapters" /> that Windows sends internet traffic through right now, or null if there's no route to the internet.
    /// That's the adapter Automatic measures: it follows a newly plugged-in cable or a VPN, and leaves out virtual adapters for Hyper-V, WSL, and Docker, whose traffic also crosses the real adapter and would otherwise be counted twice.
    /// </summary>
    public static NetworkInterface FindInternetAdapter(IReadOnlyList<NetworkInterface> adapters)
    {
        // IPv4 first, then IPv6 for networks that only have IPv6.
        foreach (var address in InternetAddresses)
        {
            if (!TryGetBestInterfaceIndex(address, out var index))
                continue;

            var adapter = adapters.FirstOrDefault(x => GetIndex(x, address.AddressFamily) == index);

            if (adapter != null)
                return adapter;
        }

        return null;
    }

    /// <summary>
    /// Returns the index of the interface Windows routes internet traffic through, and for which IP version, or null if there's no route to the internet.
    /// This only asks the routing table, so it's cheap enough to check every second.
    /// </summary>
    public static (int Index, AddressFamily Family)? GetInternetRoute()
    {
        foreach (var address in InternetAddresses)
        {
            if (TryGetBestInterfaceIndex(address, out var index))
                return (index, address.AddressFamily);
        }

        return null;
    }

    /// <summary>
    /// Returns the adapters to measure for the adapter setting <paramref name="interfaceId" />.
    /// Automatic measures <paramref name="internetAdapter" />, or every adapter when there's no route to the internet to follow.
    /// </summary>
    public static IReadOnlyList<NetworkInterface> Select(IReadOnlyList<NetworkInterface> adapters, string interfaceId, NetworkInterface internetAdapter)
    {
        if (interfaceId == AllAdapters)
            return adapters;

        if (string.IsNullOrEmpty(interfaceId))
            return internetAdapter != null ? new[] { internetAdapter } : adapters;

        return adapters.Where(x => x.Id == interfaceId).ToArray();
    }

    /// <summary>
    /// Returns a line for the tooltip saying which adapters the readings cover.
    /// </summary>
    public static string Describe(IReadOnlyList<NetworkInterface> adapters, string interfaceId, NetworkInterface internetAdapter)
    {
        if (interfaceId == AllAdapters)
            return $"Adapters: all {adapters.Count}";

        if (string.IsNullOrEmpty(interfaceId))
            return internetAdapter != null ? $"Adapter: {internetAdapter.Name} (automatic)" : $"Adapters: all {adapters.Count} (automatic)";

        return $"Adapter: {adapters.FirstOrDefault(x => x.Id == interfaceId)?.Name ?? "Disconnected"}";
    }

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

    private static bool TryGetBestInterfaceIndex(IPAddress address, out int index)
    {
        var socketAddress = new IPEndPoint(address, 0).Serialize();
        var buffer = new byte[socketAddress.Size];

        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = socketAddress[i];

        var result = GetBestInterfaceEx(buffer, out var bestIndex);
        index = (int)bestIndex;
        return result == 0;
    }

    private static int? GetIndex(NetworkInterface adapter, AddressFamily family)
    {
        try
        {
            var properties = adapter.GetIPProperties();

            return family == AddressFamily.InterNetworkV6
                ? properties.GetIPv6Properties()?.Index
                : properties.GetIPv4Properties()?.Index;
        }
        catch
        {
            // An adapter without that IP version has no index for it.
            return null;
        }
    }

    [DllImport("iphlpapi.dll")]
    private static extern int GetBestInterfaceEx(byte[] destinationAddress, out uint bestInterfaceIndex);
}
