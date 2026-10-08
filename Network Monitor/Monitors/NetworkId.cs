using System;

namespace Network_Monitor.Monitors;

/// <summary>
/// Tells one network apart from another: the adapter that reaches it, and the router at its other end.
/// </summary>
/// <remarks>
/// The router's hardware address is what tells two networks apart when both use a common router address like 192.168.1.1.
/// It's only known once Windows has looked it up, so a missing one matches either way.
/// </remarks>
public sealed class NetworkId
{
    public NetworkId(string adapterId, string gateway, string gatewayHardwareAddress)
    {
        AdapterId = adapterId;
        Gateway = gateway;
        GatewayHardwareAddress = gatewayHardwareAddress;
    }

    public string AdapterId { get; }

    /// <summary>
    /// The router's address, or null for a connection without one, like a VPN tunnel or some mobile broadband.
    /// </summary>
    public string Gateway { get; }

    /// <summary>
    /// The router's hardware (MAC) address, or null if it isn't known.
    /// </summary>
    public string GatewayHardwareAddress { get; }

    /// <summary>
    /// Whether the router is reached over IPv4. Those are preferred for telling networks apart, since IPv6 router addresses are often the same on every network.
    /// </summary>
    public bool IsIPv4 => Gateway?.IndexOf('.') >= 0;

    /// <summary>
    /// Returns whether this is the same network as <paramref name="other" />.
    /// </summary>
    public bool Matches(NetworkId other) =>
        other != null &&
        string.Equals(AdapterId, other.AdapterId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Gateway, other.Gateway, StringComparison.OrdinalIgnoreCase) &&
        (GatewayHardwareAddress is null || other.GatewayHardwareAddress is null || string.Equals(GatewayHardwareAddress, other.GatewayHardwareAddress, StringComparison.OrdinalIgnoreCase));
}
