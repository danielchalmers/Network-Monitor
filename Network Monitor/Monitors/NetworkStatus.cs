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
    /// The networks a ping has come back through, for each address pinged, which tells a network having an outage from one that blocks ping.
    /// Kept for each address, so after replies from the one pinged before, a new one that never answers, like a typo or a server that ignores ping, says it may block ping rather than waiting for a reply for good.
    /// Only kept while the app runs.
    /// </summary>
    private static readonly Dictionary<IPAddress, List<NetworkId>> RepliedNetworks = new();

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

    private static volatile Router[] _routers = Array.Empty<Router>();

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
    /// The routers to ping when the internet stops answering, to tell a drop on your own network from one past it: those of the adapter internet traffic leaves through, or while a VPN carries it, those of the network the VPN runs over.
    /// Empty when there's no route to the internet, no router to be found, like on some mobile broadband, or more than one network a VPN could run over.
    /// </summary>
    public static IReadOnlyList<Router> Routers => _routers;

    /// <summary>
    /// Whether a ping to <paramref name="address" /> has ever come back through the network internet traffic leaves through now.
    /// If so, failing pings are an outage; if not, the network may just block ping, like many work VPNs and public Wi-Fi, or the address may not answer.
    /// It's remembered per router, so a router that restarts still counts, and only a different network or address starts over.
    /// </summary>
    public static bool HasRepliedOnThisNetwork(IPAddress address)
    {
        var network = _currentNetwork;

        // Without a route there's no network to tell apart.
        if (network.Length == 0)
            return true;

        lock (RepliedNetworks)
            return RepliedNetworks.TryGetValue(address, out var replied) && GetComparable(network).Any(x => replied.Any(x.Matches));
    }

    /// <summary>
    /// Records that a ping to <paramref name="address" /> came back, and through which network.
    /// </summary>
    public static void ReportReply(IPAddress address)
    {
        Interlocked.Exchange(ref _lastReplyTimestamp, Stopwatch.GetTimestamp());

        var network = _currentNetwork;

        lock (RepliedNetworks)
        {
            if (!RepliedNetworks.TryGetValue(address, out var replied))
                RepliedNetworks[address] = replied = new List<NetworkId>();

            foreach (var id in network)
            {
                var known = replied.FindIndex(x => x.Matches(id));

                if (known < 0)
                    replied.Add(id);
                else if (replied[known].GatewayHardwareAddress is null && id.GatewayHardwareAddress != null)
                    replied[known] = id;
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
            var internetGateways = internetAdapter != null ? GetGateways(internetAdapter) : Array.Empty<(IPAddress Address, NetworkId Network)>();

            if (hasGateway && !_hasGateway && _hasRefreshed)
                Interlocked.Exchange(ref _connectedTimestamp, Stopwatch.GetTimestamp());

            _internetRoute = route;
            _currentNetwork = internetAdapter != null ? GetNetwork(internetAdapter, internetGateways) : Array.Empty<NetworkId>();
            _routers = RouterCheck.ChooseRouters(adapters, internetAdapter, x => x == internetAdapter ? internetGateways : GetGateways(x));
            _hasGateway = hasGateway;
            _hasRefreshed = true;
        }
    }

    /// <summary>
    /// Returns what identifies the network an adapter reaches: one entry per real router, or just the adapter if it has none, like a VPN tunnel.
    /// </summary>
    /// <param name="adapter">The adapter.</param>
    /// <param name="gateways">The adapter's routers, from <see cref="GetGateways" />.</param>
    private static NetworkId[] GetNetwork(NetworkInterface adapter, (IPAddress Address, NetworkId Network)[] gateways) =>
        gateways.Length > 0 ? gateways.Select(x => x.Network).ToArray() : new[] { new NetworkId(adapter.Id, null, null) };

    /// <summary>
    /// Returns an adapter's real routers, each with what tells it apart from a router with the same address on another network.
    /// </summary>
    private static (IPAddress Address, NetworkId Network)[] GetGateways(NetworkInterface adapter)
    {
        var gateways = GetGatewayAddresses(adapter).Where(IsRealGateway).Distinct().ToArray();

        if (gateways.Length == 0)
            return Array.Empty<(IPAddress Address, NetworkId Network)>();

        var ipv4Index = NetworkAdapters.GetIndex(adapter, AddressFamily.InterNetwork);

        return gateways
            .Select(x => (x, new NetworkId(
                adapter.Id,
                x.ToString(),
                x.AddressFamily == AddressFamily.InterNetwork && ipv4Index is int index ? GetHardwareAddress(x, index) : null)))
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
