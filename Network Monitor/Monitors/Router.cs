using System;
using System.Net;

namespace Network_Monitor.Monitors;

/// <summary>
/// A router to ping when pings to the internet stop coming back, which tells a drop on your own network from one past it.
/// </summary>
public sealed class Router
{
    public Router(IPAddress address, NetworkId network, string internetAdapterId)
    {
        Address = address;
        Network = network;
        InternetAdapterId = internetAdapterId;
    }

    /// <summary>
    /// The router's address, which for an IPv6 one like fe80::1%12 includes the adapter it's reached through.
    /// </summary>
    public IPAddress Address { get; }

    /// <summary>
    /// The router's adapter, address, and hardware address, which tell it apart from another router with the same address on another network.
    /// </summary>
    public NetworkId Network { get; }

    /// <summary>
    /// The adapter internet traffic leaves through: the router's own, or a VPN's that runs over it.
    /// Some VPNs keep the PC from reaching its router, so whether a router answers is remembered separately for each.
    /// </summary>
    public string InternetAdapterId { get; }

    /// <summary>
    /// Returns whether this is the same router as <paramref name="other" />, reached the same way.
    /// </summary>
    public bool Matches(Router other) =>
        other != null &&
        string.Equals(InternetAdapterId, other.InternetAdapterId, StringComparison.OrdinalIgnoreCase) &&
        Network.Matches(other.Network);
}
