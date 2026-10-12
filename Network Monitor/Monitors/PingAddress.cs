using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Network_Monitor.Monitors;

/// <summary>
/// Reads an address typed in to be pinged, which has to be a plain IPv4 address like 8.8.8.8 or IPv6 address like 2001:4860:4860::8888.
/// </summary>
/// <remarks>
/// <see cref="IPAddress.TryParse(string, out IPAddress)" /> takes far more than that, so a typo could quietly ping somewhere else:
/// "1.2.3" is 1.2.0.3, "010.1.1.1" is 8.1.1.1 because a leading zero makes it octal, and "fe80::1%12" picks an adapter.
/// Names like google.com aren't taken either, so latency never includes looking one up.
/// </remarks>
public static class PingAddress
{
    /// <summary>
    /// Reads <paramref name="text" /> as an address to ping, ignoring spaces around it.
    /// </summary>
    /// <param name="text">What was typed.</param>
    /// <param name="address">The address, or null if it can't be pinged.</param>
    /// <returns>Whether it's a plain IP address of one device, which a ping can be sent to.</returns>
    public static bool TryParse(string text, out IPAddress address)
    {
        address = null;
        text = text?.Trim();

        if (string.IsNullOrEmpty(text) || !IPAddress.TryParse(text, out var parsed))
            return false;

        // IPv4 has to be four decimal numbers with no leading zeros, and IPv6 can't have a zone, brackets, or a port.
        var isPlain = parsed.AddressFamily == AddressFamily.InterNetwork
            ? text.Split('.') is { Length: 4 } parts && parts.All(x => x.Length is >= 1 and <= 3 && x.All(c => c is >= '0' and <= '9') && (x.Length == 1 || x[0] != '0'))
            : text.IndexOfAny(new[] { '%', '[', ']' }) < 0;

        // A ping goes to one device, so not to this network itself (0.x.x.x or ::), a group of devices (multicast), or every device on the network.
        var isOneDevice = parsed.AddressFamily == AddressFamily.InterNetwork
            ? parsed.GetAddressBytes()[0] is not (0 or (>= 224 and <= 239)) && !parsed.Equals(IPAddress.Broadcast)
            : !parsed.Equals(IPAddress.IPv6Any) && !parsed.IsIPv6Multicast;

        if (!isPlain || !isOneDevice)
            return false;

        address = parsed;
        return true;
    }
}
