using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Network_Monitor.Monitors;

/// <summary>
/// Pings the router while pings to the internet aren't coming back, which tells whether a drop is on your own network or past it, like at your provider.
/// </summary>
/// <remarks>
/// Nothing is sent while replies come back, and while they don't, each router gets one ping at a time, at most one a second.
/// </remarks>
public sealed class RouterCheck
{
    private readonly Func<IPAddress, Task<bool>> _ping;
    private readonly object _lock = new();

    /// <summary>
    /// The routers being checked, and how each one's latest ping went.
    /// </summary>
    private readonly List<Target> _targets = new();

    /// <summary>
    /// The routers that have answered a ping, by the addresses <see cref="GetComparable" /> says each is known by, remembered while the app runs.
    /// Many routers never answer ping, so one that hasn't may be fine, and its silence says nothing.
    /// </summary>
    private readonly List<Router> _answered = new();

    /// <summary>
    /// Ping objects not in use, kept for the next outage rather than made every second.
    /// </summary>
    private readonly ConcurrentBag<Ping> _idlePings = new();

    private readonly int _timeout;

    /// <summary>
    /// Counts up each time replies from the internet come back, so a ping sent before then doesn't count toward the next outage.
    /// </summary>
    private int _generation;

    private bool _isChecking;

    /// <param name="timeout">How long to wait for each reply.</param>
    public RouterCheck(TimeSpan timeout)
    {
        _timeout = (int)timeout.TotalMilliseconds;
        _ping = PingAsync;
    }

    /// <param name="ping">Sends a ping and returns whether it came back, in place of a real one.</param>
    public RouterCheck(Func<IPAddress, Task<bool>> ping)
    {
        _ping = ping;
    }

    /// <summary>
    /// Pings each router that isn't still waiting for a reply.
    /// Called on the clock tick while the internet isn't answering, so a router gets at most one ping a second however long its replies take.
    /// </summary>
    /// <param name="routers">The routers internet traffic leaves through.</param>
    /// <param name="pinged">The address the latency is measured to.</param>
    public void Check(IReadOnlyList<Router> routers, IPAddress pinged)
    {
        var routersToCheck = GetRoutersToCheck(routers, pinged);
        var knownBy = GetComparable(routersToCheck);
        var toSend = new List<(Target Target, Router Router)>();
        int generation;

        lock (_lock)
        {
            _isChecking = true;
            generation = _generation;

            // The routers change with the network, like when a VPN connects.
            _targets.RemoveAll(x => !routersToCheck.Any(x.Router.Matches));

            foreach (var router in routersToCheck)
            {
                var target = _targets.Find(x => x.Router.Matches(router));

                if (target is null)
                    _targets.Add(target = new Target());

                // The newest one can have a hardware address the one before didn't.
                target.Router = router;

                if (!target.IsOut)
                {
                    target.IsOut = true;
                    toSend.Add((target, router));
                }
            }
        }

        // Sent after letting go of the lock, so handing a ping to Windows never holds up a reply from another router being noted.
        foreach (var (target, router) in toSend)
            _ = SendAsync(target, router, knownBy, generation);
    }

    /// <summary>
    /// Forgets how the routers answered during this outage, once the internet answers again, so the next one starts fresh.
    /// Which routers have ever answered is kept.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isChecking)
                return;

            _isChecking = false;
            _generation++;

            foreach (var target in _targets)
                target.Answered = null;
        }
    }

    /// <summary>
    /// Returns how the routers being checked have answered during this outage, and whether the router has answered a ping before, at any of its addresses.
    /// </summary>
    public (RouterReply Reply, bool HasAnswered) GetState()
    {
        lock (_lock)
            return (Summarize(_targets.Select(x => x.Answered)), GetComparable(_targets.Select(x => x.Router).ToArray()).Any(x => _answered.Any(x.Matches)));
    }

    /// <summary>
    /// Returns whether to ping the router: while the outage clock is up, and from a second before, once a reply is overdue and the last number is dimmed.
    /// Starting early means a router that answers has done so by the time the clock shows, and that a ping or two lost on a healthy connection is enough to find out whether the router answers ping at all.
    /// Not while replies come back, nor on a network that seems to block ping, where the internet never answers and the router would be pinged for good.
    /// </summary>
    /// <param name="view">What the latency reading shows.</param>
    /// <param name="secondsSinceReply">Whole seconds since the newest second whose ping got a reply, or null if none has since the app started or the PC woke up.</param>
    public static bool IsNeeded(LatencyView view, long? secondsSinceReply) =>
        view.IsOutage || (view.IsStale && secondsSinceReply >= Outage.ClockAfterSeconds - 1);

    /// <summary>
    /// Returns the routers worth pinging when <paramref name="pinged" /> stops answering: none when it's one of them, since then the router is what the latency is measured to.
    /// </summary>
    public static IReadOnlyList<Router> GetRoutersToCheck(IReadOnlyList<Router> routers, IPAddress pinged)
    {
        // The address picked can't have a zone like %12, but a router's IPv6 address does, so only the address itself is compared.
        var pingedBytes = pinged?.GetAddressBytes();

        return pingedBytes != null && routers.Any(x => x.Address.GetAddressBytes().SequenceEqual(pingedBytes))
            ? Array.Empty<Router>()
            : routers;
    }

    /// <summary>
    /// Returns the routers to ping when the internet stops answering: those of the adapter internet traffic leaves through, or while that's a VPN, those of the network the VPN runs over, if there's only one it could be.
    /// A VPN's own gateway, when it has one, is its server out on the internet, so whether it answers can't tell your own network from your provider.
    /// </summary>
    /// <param name="adapters">The connected adapters.</param>
    /// <param name="internetAdapter">The adapter internet traffic leaves through, or null if there's no route to the internet.</param>
    /// <param name="getGateways">Returns an adapter's routers, each with what tells it apart from other routers.</param>
    public static Router[] ChooseRouters(IEnumerable<NetworkInterface> adapters, NetworkInterface internetAdapter, Func<NetworkInterface, IEnumerable<(IPAddress Address, NetworkId Network)>> getGateways)
    {
        if (internetAdapter is null)
            return Array.Empty<Router>();

        var gateways = NetworkAdapters.IsTunnel(internetAdapter)
            ? Array.Empty<(IPAddress Address, NetworkId Network)>()
            : getGateways(internetAdapter).ToArray();

        // A VPN, or a connection without a router of its own, which is almost always a VPN too.
        if (gateways.Length == 0)
        {
            var networks = adapters
                .Where(x => x.Id != internetAdapter.Id && !NetworkAdapters.IsTunnel(x))
                .Select(x => getGateways(x).ToArray())
                .Where(x => x.Length > 0)
                .ToArray();

            // Which one it runs over isn't known, so with more than one network, like Ethernet and a phone's hotspot over Wi-Fi, one router answering wouldn't say the other is fine.
            // Ethernet and Wi-Fi on the same network reach the same router, so that's still checked.
            if (networks.Skip(1).Any(x => !HaveSameRouter(networks[0], x)))
                return Array.Empty<Router>();

            gateways = networks.SelectMany(x => x).ToArray();
        }

        return gateways.Select(x => new Router(x.Address, x.Network, internetAdapter.Id)).ToArray();
    }

    /// <summary>
    /// Returns how the routers answered, from how each one's latest ping went: true if it came back, false if it didn't, or null if none has finished yet.
    /// One router answering is enough, since a PC often has more than one address for the same router, like an IPv4 and an IPv6 one, and not every one answers ping.
    /// </summary>
    public static RouterReply Summarize(IEnumerable<bool?> latest)
    {
        var any = false;
        var isWaiting = false;

        foreach (var answered in latest)
        {
            if (answered == true)
                return RouterReply.Answering;

            any = true;
            isWaiting |= answered is null;
        }

        if (!any)
            return RouterReply.None;

        return isWaiting ? RouterReply.Waiting : RouterReply.NotAnswering;
    }

    /// <summary>
    /// Returns a tooltip line about the router and what a screen reader says, or null when there's nothing to say.
    /// </summary>
    /// <param name="reply">How the router has answered during this outage.</param>
    /// <param name="hasAnswered">Whether the router has answered a ping before, at any time since the app started.</param>
    /// <param name="isOutage">Whether the internet has stopped answering for long enough to show the outage clock.</param>
    public static (string Note, string Spoken)? Describe(RouterReply reply, bool hasAnswered, bool isOutage)
    {
        if (!isOutage)
            return null;

        return reply switch
        {
            // Not "the internet", since the address picked to ping may be what's down.
            RouterReply.Answering => ("Your router is fine; the problem is past it", "your router is fine, the problem is past it"),
            RouterReply.Waiting => ("Checking your router…", "checking your router"),
            RouterReply.NotAnswering when hasAnswered => ("Your router isn't answering", "your router isn't answering"),

            // It may just not answer ping, like many routers, so it isn't blamed.
            RouterReply.NotAnswering => ("Can't tell if it's your router; it hasn't answered a ping yet", "can't tell if it's your router"),

            // With no router to check, there's nothing to tell. No router at all already reads "No network connection".
            _ => null,
        };
    }

    /// <summary>
    /// Returns the addresses to know a router by from one network to the next: its IPv4 ones if it has any, which come with its hardware address, as when <see cref="NetworkStatus" /> tells networks apart.
    /// IPv6 router addresses are often the same on every network, like fe80::1, so another network's router with the same one hasn't answered just because this one has.
    /// </summary>
    private static IReadOnlyList<Router> GetComparable(IReadOnlyList<Router> routers) =>
        routers.Any(x => x.Network.IsIPv4) ? routers.Where(x => x.Network.IsIPv4).ToArray() : routers;

    /// <summary>
    /// Returns whether two adapters reach the same router, by an address and hardware address they share.
    /// An IPv6 router address like fe80::1%12 includes its adapter, so it's the IPv4 one that's shared.
    /// </summary>
    private static bool HaveSameRouter((IPAddress Address, NetworkId Network)[] a, (IPAddress Address, NetworkId Network)[] b) =>
        a.Any(x => b.Any(y => x.Network.HasSameRouter(y.Network)));

    /// <summary>
    /// Pings one router and notes how it went.
    /// It never fails, so nothing waits for the task it returns.
    /// </summary>
    /// <param name="target">Where to note it.</param>
    /// <param name="router">The router, as it was when the ping was sent.</param>
    /// <param name="knownBy">What to remember the router by if it answers, from <see cref="GetComparable" />, as it was when the ping was sent.</param>
    /// <param name="generation">The outage the ping was sent in.</param>
    private async Task SendAsync(Target target, Router router, IReadOnlyList<Router> knownBy, int generation)
    {
        var answered = false;

        try
        {
            answered = await _ping(router.Address).ConfigureAwait(false);
        }
        catch
        {
            // A ping that can't be sent, like with no route to the router, got no answer.
        }

        lock (_lock)
        {
            target.IsOut = false;

            // One of its addresses answering is the whole router answering, as it is for the outage, so a router that only answers on IPv6 is still known by its IPv4 address.
            if (answered)
            {
                foreach (var known in knownBy)
                    Remember(known);
            }

            if (generation == _generation)
                target.Answered = answered;
        }
    }

    /// <summary>
    /// Notes that a router answered, keeping its hardware address once it's known.
    /// </summary>
    private void Remember(Router router)
    {
        var known = _answered.FindIndex(x => x.Matches(router));

        if (known < 0)
            _answered.Add(router);
        else if (_answered[known].Network.GatewayHardwareAddress is null && router.Network.GatewayHardwareAddress != null)
            _answered[known] = router;
    }

    /// <summary>
    /// Sends one ping and returns whether it came back.
    /// </summary>
    private async Task<bool> PingAsync(IPAddress address)
    {
        if (!_idlePings.TryTake(out var ping))
            ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(address, _timeout).ConfigureAwait(false);
            _idlePings.Add(ping);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            // As with the latency's own pings, one that failed this way isn't reused.
            ping.Dispose();
            return false;
        }
    }

    /// <summary>
    /// A router being checked.
    /// Only touched while holding the lock.
    /// </summary>
    private sealed class Target
    {
        public Router Router { get; set; }

        /// <summary>
        /// Whether a ping to it is still waiting for a reply.
        /// </summary>
        public bool IsOut { get; set; }

        /// <summary>
        /// Whether its latest ping during this outage came back, or null if none has finished yet.
        /// </summary>
        public bool? Answered { get; set; }
    }
}

/// <summary>
/// How the router has answered while the internet hasn't.
/// </summary>
public enum RouterReply
{
    /// <summary>
    /// There's no router to check, or it's the address the latency is measured to.
    /// </summary>
    None,

    /// <summary>
    /// No ping to it has finished yet.
    /// </summary>
    Waiting,

    /// <summary>
    /// It answered its latest ping.
    /// </summary>
    Answering,

    /// <summary>
    /// Its latest ping got no answer.
    /// </summary>
    NotAnswering,
}
