using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Network_Monitor.Monitors;

/// <summary>
/// Whether the PC is connected to a network at all, kept up to date from network-change events rather than checked every second.
/// </summary>
/// <remarks>
/// The check the monitors used before took a few milliseconds and ran three times a second, which was most of the widget's idle CPU.
/// It also counted Hyper-V, WSL, and other virtual adapters as a connection, so the offline state never showed on PCs that have them.
/// </remarks>
public static class NetworkStatus
{
    /// <summary>
    /// How often to check again in case a network-change event was missed.
    /// </summary>
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A reply this recent proves there's a connection whatever the adapters say.
    /// It's under the outage clock's three seconds, so a lost connection says so rather than flashing the clock first.
    /// </summary>
    private static readonly long RecentReplyTicks = Stopwatch.Frequency * 5 / 2;

    /// <summary>
    /// How long after a connection comes back that failed pings are put down to it still settling.
    /// </summary>
    private static readonly long ReconnectingTicks = 5 * Stopwatch.Frequency;

    /// <summary>
    /// The networks a ping has come back through, which tells a network having an outage from one that blocks ping.
    /// Only kept while the app runs.
    /// </summary>
    private static readonly List<NetworkId> RepliedNetworks = new();

    /// <summary>
    /// Keeps a network-change event, the recheck timer, and the clock tick from refreshing at the same time and leaving an older result behind.
    /// </summary>
    private static readonly object RefreshLock = new();

    private static readonly Timer RecheckTimer;
    private static volatile bool _hasGateway;
    private static bool _hasRefreshed;

    /// <summary>
    /// The network internet traffic leaves through now: one entry per router of the adapter Windows routes it through.
    /// </summary>
    private static volatile NetworkId[] _currentNetwork = Array.Empty<NetworkId>();

    private static (int Index, AddressFamily Family)? _internetRoute;

    /// <summary>
    /// When the PC last went from no gateway to having one, in <see cref="Stopwatch" /> ticks, or zero if it hasn't since the app started.
    /// </summary>
    private static long _connectedTimestamp;

    /// <summary>
    /// When the last ping came back, in <see cref="Stopwatch" /> ticks, or zero if none has.
    /// Written from ping completions and read from the clock tick, so it's only accessed atomically.
    /// </summary>
    private static long _lastReplyTimestamp;

    static NetworkStatus()
    {
        Refresh();

        NetworkChange.NetworkAddressChanged += (_, _) => Refresh();
        NetworkChange.NetworkAvailabilityChanged += (_, _) => Refresh();
        RecheckTimer = new Timer(_ => Refresh(), null, RecheckInterval, RecheckInterval);
    }

    /// <summary>
    /// Whether no adapter has a way out of this PC and no ping has come back lately, meaning there's no network to measure.
    /// A recent reply counts too, since some connections, like dial-up style mobile broadband, don't report a gateway.
    /// </summary>
    public static bool IsOffline => !_hasGateway && !HasRecentReply;

    private static bool HasRecentReply
    {
        get
        {
            var last = Interlocked.Read(ref _lastReplyTimestamp);
            return last != 0 && Stopwatch.GetTimestamp() - last < RecentReplyTicks;
        }
    }

    /// <summary>
    /// Whether the PC got its connection back moments ago, when pings often fail for a few seconds while it settles.
    /// </summary>
    public static bool IsReconnecting
    {
        get
        {
            var connected = Interlocked.Read(ref _connectedTimestamp);
            return _hasGateway && connected != 0 && Stopwatch.GetTimestamp() - connected < ReconnectingTicks;
        }
    }

    /// <summary>
    /// Whether a ping has ever come back through the network internet traffic leaves through now.
    /// If so, failing pings are an outage; if not, the network may just block ping, like many work VPNs and public Wi-Fi.
    /// It's remembered per router, so a router that restarts still counts, and only a different network starts over.
    /// </summary>
    public static bool HasRepliedOnThisNetwork
    {
        get
        {
            var network = _currentNetwork;

            // Without a route there's no network to tell apart.
            if (network.Length == 0)
                return true;

            lock (RepliedNetworks)
                return GetComparable(network).Any(x => RepliedNetworks.Any(x.Matches));
        }
    }

    /// <summary>
    /// Records that a ping came back, and through which network.
    /// </summary>
    public static void ReportReply()
    {
        Interlocked.Exchange(ref _lastReplyTimestamp, Stopwatch.GetTimestamp());

        var network = _currentNetwork;

        lock (RepliedNetworks)
        {
            foreach (var id in network)
            {
                var known = RepliedNetworks.FindIndex(x => x.Matches(id));

                if (known < 0)
                    RepliedNetworks.Add(id);
                else if (RepliedNetworks[known].GatewayHardwareAddress is null && id.GatewayHardwareAddress != null)
                    RepliedNetworks[known] = id;
            }
        }
    }

    /// <summary>
    /// Checks whether the route to the internet has moved to another adapter, which can happen without a network-change event, like when a VPN adds its routes after connecting.
    /// Called on the clock tick; the lookup is cheap, and the adapters are only listed again when the route has moved.
    /// </summary>
    public static void FollowInternetRoute()
    {
        var route = NetworkAdapters.GetInternetRoute();
        bool hasMoved;

        lock (RefreshLock)
            hasMoved = !Equals(route, _internetRoute);

        if (hasMoved)
            Refresh();
    }

    /// <summary>
    /// Returns the routers to compare networks by: the IPv4 ones if there are any, since IPv6 router addresses are often the same on every network.
    /// </summary>
    private static IEnumerable<NetworkId> GetComparable(NetworkId[] network) =>
        network.Any(x => x.IsIPv4) ? network.Where(x => x.IsIPv4) : network;

    private static void Refresh()
    {
        lock (RefreshLock)
        {
            NetworkInterface[] adapters;

            try
            {
                adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToArray();
            }
            catch (NetworkInformationException)
            {
                // Keep the last known state rather than guessing.
                return;
            }

            var hasGateway = adapters.SelectMany(GetGatewayAddresses).Any(IsRealGateway);
            var route = NetworkAdapters.GetInternetRoute();
            var internetAdapter = route is (int index, AddressFamily family) ? NetworkAdapters.FindByIndex(adapters, index, family) : null;

            if (hasGateway && !_hasGateway && _hasRefreshed)
                Interlocked.Exchange(ref _connectedTimestamp, Stopwatch.GetTimestamp());

            _internetRoute = route;
            _currentNetwork = internetAdapter != null ? GetNetwork(internetAdapter) : Array.Empty<NetworkId>();
            _hasGateway = hasGateway;
            _hasRefreshed = true;
        }
    }

    /// <summary>
    /// Returns what identifies the network an adapter reaches: one entry per real router, or just the adapter if it has none, like a VPN tunnel.
    /// </summary>
    private static NetworkId[] GetNetwork(NetworkInterface adapter)
    {
        var gateways = GetGatewayAddresses(adapter).Where(IsRealGateway).Distinct().ToArray();

        if (gateways.Length == 0)
            return new[] { new NetworkId(adapter.Id, null, null) };

        var ipv4Index = NetworkAdapters.GetIndex(adapter, AddressFamily.InterNetwork);

        return gateways
            .Select(x => new NetworkId(
                adapter.Id,
                x.ToString(),
                x.AddressFamily == AddressFamily.InterNetwork && ipv4Index is int index ? GetHardwareAddress(x, index) : null))
            .ToArray();
    }

    private static IEnumerable<IPAddress> GetGatewayAddresses(NetworkInterface adapter)
    {
        try
        {
            return adapter.GetIPProperties().GatewayAddresses.Select(x => x.Address).ToArray();
        }
        catch
        {
            // Some adapter types don't support IP properties.
            return Array.Empty<IPAddress>();
        }
    }

    /// <summary>
    /// Returns a router's hardware address from the list of neighbors Windows already knows, or null if it isn't there.
    /// This only reads that list, so it never sends anything or waits on the network.
    /// </summary>
    private static string GetHardwareAddress(IPAddress gateway, int interfaceIndex)
    {
        var size = 0;

        if (GetIpNetTable(IntPtr.Zero, ref size, false) != ERROR_INSUFFICIENT_BUFFER || size <= 0)
            return null;

        var table = Marshal.AllocHGlobal(size);

        try
        {
            if (GetIpNetTable(table, ref size, false) != 0)
                return null;

            var target = BitConverter.ToUInt32(gateway.GetAddressBytes(), 0);
            var count = Marshal.ReadInt32(table);

            // Each MIB_IPNETROW: index, address length, 8 bytes of hardware address, IPv4 address, entry type.
            for (var i = 0; i < count; i++)
            {
                var row = IntPtr.Add(table, 4 + (i * 24));
                var length = Math.Min(Marshal.ReadInt32(row, 4), 8);

                if (Marshal.ReadInt32(row) != interfaceIndex || (uint)Marshal.ReadInt32(row, 16) != target || Marshal.ReadInt32(row, 20) == MIB_IPNET_TYPE_INVALID || length <= 0)
                    continue;

                var address = new byte[length];
                Marshal.Copy(IntPtr.Add(row, 8), address, 0, length);

                return address.All(x => x == 0) ? null : BitConverter.ToString(address);
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    /// <summary>
    /// Returns whether a gateway address actually leads somewhere, rather than being a placeholder.
    /// </summary>
    public static bool IsRealGateway(IPAddress address) =>
        address != null && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any);

    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int MIB_IPNET_TYPE_INVALID = 2;

    [DllImport("iphlpapi.dll")]
    private static extern int GetIpNetTable(IntPtr ipNetTable, ref int size, bool order);
}
