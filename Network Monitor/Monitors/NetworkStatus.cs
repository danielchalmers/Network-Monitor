using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
    /// </summary>
    private static readonly long RecentReplyTicks = 3 * Stopwatch.Frequency;

    private static readonly Timer RecheckTimer;
    private static volatile bool _hasGateway;

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
    /// Records that a ping came back.
    /// </summary>
    public static void ReportReply() => Interlocked.Exchange(ref _lastReplyTimestamp, Stopwatch.GetTimestamp());

    private static void Refresh()
    {
        try
        {
            _hasGateway = NetworkInterface.GetAllNetworkInterfaces().Any(HasRealGateway);
        }
        catch (NetworkInformationException)
        {
            // Keep the last known state rather than guessing.
        }
    }

    /// <summary>
    /// Returns whether an adapter is up and has a default gateway, which is what connects it to anything beyond this PC.
    /// Virtual switches for Hyper-V, WSL, and Docker don't have one, and VPN tunnels that report an unspecified gateway (0.0.0.0) aren't counted on their own.
    /// </summary>
    private static bool HasRealGateway(NetworkInterface adapter)
    {
        try
        {
            return adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                adapter.GetIPProperties().GatewayAddresses.Any(x => IsRealGateway(x.Address));
        }
        catch
        {
            // Some adapter types don't support IP properties.
            return false;
        }
    }

    /// <summary>
    /// Returns whether a gateway address actually leads somewhere, rather than being a placeholder.
    /// </summary>
    public static bool IsRealGateway(IPAddress address) =>
        address != null && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any);
}
