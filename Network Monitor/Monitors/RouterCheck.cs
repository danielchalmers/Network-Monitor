using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Network_Monitor.Monitors;

/// <summary>
/// Pings the router during an outage, which tells whether a drop is on your own network or past it, like at your provider.
/// </summary>
/// <remarks>
/// Nothing is sent while the internet answers, and during an outage the router gets one ping at a time, at most one a second.
/// </remarks>
public sealed class RouterCheck
{
    private readonly Func<string, Task<bool>> _ping;
    private readonly object _lock = new();

    /// <summary>
    /// Whether the router's latest ping during this outage came back, or null if none has finished yet.
    /// </summary>
    private bool? _answered;

    private bool _isOut;

    /// <param name="timeout">How long to wait for each reply.</param>
    public RouterCheck(TimeSpan timeout) : this(address => PingAsync(address, (int)timeout.TotalMilliseconds))
    {
    }

    /// <param name="ping">Sends a ping and returns whether it came back, in place of a real one.</param>
    public RouterCheck(Func<string, Task<bool>> ping)
    {
        _ping = ping;
    }

    /// <summary>
    /// Pings <paramref name="router" /> unless it's still waiting for a reply, and returns a tooltip line about it and what a screen reader says.
    /// Called on the clock tick during an outage.
    /// </summary>
    /// <param name="router">The IPv4 address of the router internet traffic leaves through, or null if there's none.</param>
    /// <returns>Null when there's no router to check.</returns>
    public (string Note, string Spoken)? Check(string router)
    {
        if (router is null)
            return null;

        lock (_lock)
        {
            if (!_isOut)
            {
                _isOut = true;
                _ = SendAsync(router);
            }

            return _answered switch
            {
                // Not "the internet", since 8.8.8.8 itself may be what's down.
                true => ("Your router is fine; the problem is past it", "your router is fine, the problem is past it"),
                false => ("Your router isn't answering", "your router isn't answering"),
                null => ("Checking your router…", "checking your router"),
            };
        }
    }

    /// <summary>
    /// Forgets how the router answered during this outage, once the internet answers again, so the next one starts fresh.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
            _answered = null;
    }

    /// <summary>
    /// Pings the router and notes how it went.
    /// It never fails, so nothing waits for the task it returns.
    /// </summary>
    private async Task SendAsync(string router)
    {
        var answered = false;

        try
        {
            answered = await _ping(router).ConfigureAwait(false);
        }
        catch
        {
            // A ping that can't be sent, like with no route to the router, got no answer.
        }

        lock (_lock)
        {
            _isOut = false;
            _answered = answered;
        }
    }

    private static async Task<bool> PingAsync(string address, int timeout)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(address, timeout).ConfigureAwait(false);
        return reply.Status == IPStatus.Success;
    }
}
