using System;
using System.Net;
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
    private readonly Func<IPAddress, Task<bool>> _ping;
    private readonly object _lock = new();

    /// <summary>
    /// The router being checked, or null if there's none.
    /// </summary>
    private NetworkId _router;

    /// <summary>
    /// The router that last answered a ping, remembered while the app runs.
    /// Many routers never answer ping, so one that hasn't may be fine, and its silence says nothing.
    /// </summary>
    private NetworkId _answeredRouter;

    /// <summary>
    /// Whether the router's latest ping during this outage came back, or null if none has finished yet.
    /// </summary>
    private bool? _answered;

    private bool _isOut;

    /// <summary>
    /// Counts up at the end of each outage, so a ping sent during one doesn't count toward the next.
    /// </summary>
    private int _generation;

    /// <param name="timeout">How long to wait for each reply.</param>
    public RouterCheck(TimeSpan timeout) : this(address => PingAsync(address, (int)timeout.TotalMilliseconds))
    {
    }

    /// <param name="ping">Sends a ping and returns whether it came back, in place of a real one.</param>
    public RouterCheck(Func<IPAddress, Task<bool>> ping)
    {
        _ping = ping;
    }

    /// <summary>
    /// Pings <paramref name="router" /> unless it's still waiting for a reply.
    /// Called on the clock tick during an outage.
    /// </summary>
    /// <param name="router">The router internet traffic leaves through, or null if there's none worth checking.</param>
    public void Check(NetworkId router)
    {
        int generation;

        lock (_lock)
        {
            // The router changes with the network, like when moving from home to a café.
            if (router is null || !router.Matches(_router))
                _answered = null;

            _router = router;

            if (router is null || _isOut)
                return;

            _isOut = true;
            generation = _generation;
        }

        _ = SendAsync(router, generation);
    }

    /// <summary>
    /// Forgets how the router answered during this outage, once the internet answers again, so the next one starts fresh.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _generation++;
            _router = null;
            _answered = null;
        }
    }

    /// <summary>
    /// Returns a tooltip line about the router and what a screen reader says, or null when there's nothing to say.
    /// </summary>
    public (string Note, string Spoken)? Describe()
    {
        lock (_lock)
        {
            if (_router is null)
                return null;

            return _answered switch
            {
                // Not "the internet", since the address picked to ping may be what's down.
                true => ("Your router is fine; the problem is past it", "your router is fine, the problem is past it"),
                null => ("Checking your router…", "checking your router"),
                false when _router.Matches(_answeredRouter) => ("Your router isn't answering", "your router isn't answering"),

                // It may just not answer ping, like many routers, so it isn't blamed.
                false => ("Can't tell if it's your router; it hasn't answered a ping yet", "can't tell if it's your router"),
            };
        }
    }

    /// <summary>
    /// Pings the router and notes how it went.
    /// It never fails, so nothing waits for the task it returns.
    /// </summary>
    private async Task SendAsync(NetworkId router, int generation)
    {
        var answered = false;

        try
        {
            answered = await _ping(IPAddress.Parse(router.Gateway)).ConfigureAwait(false);
        }
        catch
        {
            // A ping that can't be sent, like with no route to the router, got no answer.
        }

        lock (_lock)
        {
            _isOut = false;

            if (answered)
                _answeredRouter = router;

            if (generation == _generation && router.Matches(_router))
                _answered = answered;
        }
    }

    private static async Task<bool> PingAsync(IPAddress address, int timeout)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(address, timeout).ConfigureAwait(false);
        return reply.Status == IPStatus.Success;
    }
}
