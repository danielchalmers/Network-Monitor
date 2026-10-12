using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Network_Monitor.Monitors;

/// <summary>
/// Reads an address typed in to be pinged, which has to be a plain IPv4 address like 8.8.8.8 or IPv6 address like 2001:4860:4860::8888.
/// </summary>
/// <remarks>
/// <see cref="IPAddress.TryParse(string, out IPAddress)" /> takes far more than that, so a typo could quietly ping somewhere else:
/// "1" is 0.0.0.1, "1.2.3" is 1.2.0.3, "0x7f.1" is 127.0.0.1, "010.1.1.1" is 8.1.1.1 because a leading zero makes it octal, and "fe80::1%12" picks an adapter.
/// Names like google.com aren't taken either, so latency never includes looking one up, and a name that moves to another server can't make the reading jump.
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

        if (string.IsNullOrEmpty(text))
            return false;

        var bytes = text.IndexOf(':') >= 0 ? ParseIPv6(text) : ParseIPv4(text);

        if (bytes is null || !CanBePinged(bytes))
            return false;

        address = new IPAddress(bytes);
        return true;
    }

    /// <summary>
    /// Returns the four numbers of an IPv4 address, or null if <paramref name="text" /> isn't one written out in full.
    /// Each number has to be decimal, from 0 to 255, with no leading zeros, which some programs read as octal.
    /// </summary>
    private static byte[] ParseIPv4(string text)
    {
        var parts = text.Split('.');

        if (parts.Length != 4)
            return null;

        var bytes = new byte[4];

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.Length is < 1 or > 3 || (part.Length > 1 && part[0] == '0') || !part.All(IsDecimalDigit))
                return null;

            var value = part.Aggregate(0, (total, digit) => (total * 10) + (digit - '0'));

            if (value > 255)
                return null;

            bytes[i] = (byte)value;
        }

        return bytes;
    }

    /// <summary>
    /// Returns the sixteen bytes of an IPv6 address, or null if <paramref name="text" /> isn't one.
    /// It's eight groups of one to four hex digits, with "::" standing in for one or more groups of zeros, and the last two groups can be written as an IPv4 address, like 64:ff9b::8.8.8.8.
    /// A zone like "%12", brackets, or a port aren't taken.
    /// </summary>
    private static byte[] ParseIPv6(string text)
    {
        var halves = text.Split(new[] { "::" }, StringSplitOptions.None);

        if (halves.Length > 2)
            return null;

        var hasGap = halves.Length == 2;
        var head = ParseGroups(halves[0], allowIPv4: !hasGap);
        var tail = hasGap ? ParseGroups(halves[1], allowIPv4: true) : new List<ushort>();

        if (head is null || tail is null)
            return null;

        var count = head.Count + tail.Count;

        // Without "::" every group is written out, and with it at least one is left out.
        if (hasGap ? count > 7 : count != 8)
            return null;

        return head
            .Concat(Enumerable.Repeat((ushort)0, 8 - count))
            .Concat(tail)
            .SelectMany(x => new[] { (byte)(x >> 8), (byte)x })
            .ToArray();
    }

    /// <summary>
    /// Returns the groups on one side of an IPv6 address's "::", or null if they aren't all one to four hex digits.
    /// An empty side has none.
    /// </summary>
    /// <param name="allowIPv4">Whether the last group can be an IPv4 address, which only goes at the very end of the address.</param>
    private static List<ushort> ParseGroups(string text, bool allowIPv4)
    {
        var groups = new List<ushort>();

        if (text.Length == 0)
            return groups;

        var parts = text.Split(':');

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (allowIPv4 && i == parts.Length - 1 && part.IndexOf('.') >= 0)
            {
                if (ParseIPv4(part) is not byte[] ipv4)
                    return null;

                groups.Add((ushort)((ipv4[0] << 8) | ipv4[1]));
                groups.Add((ushort)((ipv4[2] << 8) | ipv4[3]));
            }
            else if (part.Length is >= 1 and <= 4 && part.All(IsHexDigit))
            {
                groups.Add((ushort)part.Aggregate(0, (total, digit) => (total * 16) + GetHexValue(digit)));
            }
            else
            {
                return null;
            }
        }

        return groups;
    }

    /// <summary>
    /// Returns whether an address stands for one device, which can answer a ping.
    /// </summary>
    private static bool CanBePinged(byte[] bytes)
    {
        if (bytes.Length == 4)
        {
            // 0.x.x.x means this network itself and is never sent to, 224 to 239 are multicast, which goes to a group of devices, and 255.255.255.255 goes to every device on the network.
            return bytes[0] != 0 && bytes[0] is not (>= 224 and <= 239) && !bytes.All(x => x == 255);
        }

        // :: means no address at all, ff00::/8 is multicast, and ::ffff:8.8.8.8 is an IPv4 address written for IPv6 programs, which isn't sent over the network in that form, so it's typed as 8.8.8.8 instead.
        // The same goes for ::8.8.8.8, an older way of writing one that's no longer used, so ::1, which is this PC, is the only address starting with 96 zero bits that's taken.
        var isUnspecified = bytes.All(x => x == 0);
        var isLoopback = bytes.Take(15).All(x => x == 0) && bytes[15] == 1;
        var isMulticast = bytes[0] == 0xFF;
        var isIPv4Mapped = bytes.Take(10).All(x => x == 0) && bytes[10] == 0xFF && bytes[11] == 0xFF;
        var isIPv4Compatible = bytes.Take(12).All(x => x == 0) && !isUnspecified && !isLoopback;

        return !isUnspecified && !isMulticast && !isIPv4Mapped && !isIPv4Compatible;
    }

    /// <summary>
    /// Returns whether <paramref name="c" /> is 0 to 9, leaving out the digits of other scripts, like Arabic-Indic or full-width ones, that <see cref="char.IsDigit(char)" /> takes.
    /// </summary>
    private static bool IsDecimalDigit(char c) => c is >= '0' and <= '9';

    private static bool IsHexDigit(char c) => GetHexValue(c) >= 0;

    private static int GetHexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}
