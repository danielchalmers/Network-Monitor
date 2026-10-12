using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class RouterCheckTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 14, 4, 0, DateTimeKind.Utc);
    private static readonly IPAddress Internet = IPAddress.Parse("8.8.8.8");
    private static readonly Router Home = new(IPAddress.Parse("192.168.1.1"), new NetworkId("wifi", "192.168.1.1", "AA-BB-CC-00-11-22"), "wifi");
    private static readonly Router HomeIPv6 = new(IPAddress.Parse("fe80::1%12"), new NetworkId("wifi", "fe80::1%12", null), "wifi");

    private static readonly FakeAdapter WiFi = new("wifi", NetworkInterfaceType.Wireless80211, "Intel(R) Wi-Fi 6 AX201 160MHz");
    private static readonly FakeAdapter Ethernet = new("ethernet", NetworkInterfaceType.Ethernet, "Realtek PCIe GbE Family Controller");
    private static readonly FakeAdapter Dock = new("dock", NetworkInterfaceType.Ethernet, "Realtek USB GbE Family Controller");
    private static readonly FakeAdapter HyperV = new("hyperv", NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter");
    private static readonly FakeAdapter ExternalSwitch = new("external", NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter #2");
    private static readonly FakeAdapter WireGuard = new("wireguard", (NetworkInterfaceType)53, "WireGuard Tunnel");
    private static readonly FakeAdapter OpenVpn = new("openvpn", NetworkInterfaceType.Ethernet, "TAP-Windows Adapter V9");
    private static readonly FakeAdapter WindowsVpn = new("ras", NetworkInterfaceType.Ppp, "WAN Miniport (IKEv2)");

    private static readonly Dictionary<string, (IPAddress, NetworkId)[]> Gateways = new()
    {
        ["wifi"] = new[] { Gateway("wifi", "192.168.1.1", "AA-BB-CC-00-11-22"), Gateway("wifi", "fe80::1%12") },
        ["ethernet"] = new[] { Gateway("ethernet", "10.0.0.1") },
        ["dock"] = new[] { Gateway("dock", "192.168.1.1", "AA-BB-CC-00-11-22") },
        ["external"] = new[] { Gateway("external", "192.168.0.1") },
        ["openvpn"] = new[] { Gateway("openvpn", "10.8.0.1") },
        ["ras"] = new[] { Gateway("ras", "172.16.0.1") },
    };

    [Fact]
    public void IsNeeded_WhileRepliesComeBack_IsNot()
    {
        Assert.False(RouterCheck.IsNeeded(LatencyView.Reading(20), 1));
    }

    [Fact]
    public void IsNeeded_OnceAReplyIsOverdue_Is()
    {
        // A second before the clock, so a router that answers has done so by the time it shows.
        Assert.False(RouterCheck.IsNeeded(LatencyView.Dimmed(20), 1));
        Assert.True(RouterCheck.IsNeeded(LatencyView.Dimmed(20), 2));
    }

    [Fact]
    public void IsNeeded_WhileTheClockIsUp_Is()
    {
        Assert.True(RouterCheck.IsNeeded(LatencyView.Clock(TimeSpan.FromSeconds(3), Now.AddSeconds(-3), Now), 3));
        Assert.True(RouterCheck.IsNeeded(LatencyView.Clock(TimeSpan.FromHours(2), Now.AddHours(-2), Now), 7200));
    }

    [Fact]
    public void IsNeeded_OnANetworkThatSeemsToBlockPing_IsNot()
    {
        // The internet never answers there, so the router would be pinged every second for as long as the PC is on it.
        Assert.False(RouterCheck.IsNeeded(LatencyView.BlockedNetwork(), 30));
    }

    [Fact]
    public void IsNeeded_WhileReconnectingOrAfterWaking_IsNot()
    {
        Assert.False(RouterCheck.IsNeeded(LatencyView.Note("Reconnecting…", "reconnecting"), 40));
        Assert.False(RouterCheck.IsNeeded(LatencyView.Note("Waiting for a reply", "waiting for a reply"), null));
    }

    [Fact]
    public void Describe_WhenTheRouterAnswers_SaysTheDropIsPastIt()
    {
        // Not "the internet isn't answering", since the address picked to ping may be what's down.
        Assert.Equal(("Your router is fine; the problem is past it", "your router is fine, the problem is past it"), RouterCheck.Describe(RouterReply.Answering, hasAnswered: true, isOutage: true));
    }

    [Fact]
    public void Describe_WhenARouterThatHasAnsweredBeforeDoesnt_SaysSo()
    {
        Assert.Equal(("Your router isn't answering", "your router isn't answering"), RouterCheck.Describe(RouterReply.NotAnswering, hasAnswered: true, isOutage: true));
    }

    [Fact]
    public void Describe_WhenTheRouterHasNeverAnswered_CantTell()
    {
        // Many routers don't answer ping however well they're working, so this one isn't blamed.
        Assert.Equal(("Can't tell if it's your router; it hasn't answered a ping yet", "can't tell if it's your router"), RouterCheck.Describe(RouterReply.NotAnswering, hasAnswered: false, isOutage: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Describe_BeforeTheRoutersFirstPingFinishes_SaysItsChecking(bool hasAnswered)
    {
        Assert.Equal(("Checking your router…", "checking your router"), RouterCheck.Describe(RouterReply.Waiting, hasAnswered, isOutage: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Describe_WithNoRouterToCheck_SaysNothing(bool hasAnswered)
    {
        Assert.Null(RouterCheck.Describe(RouterReply.None, hasAnswered, isOutage: true));
    }

    [Theory]
    [InlineData(RouterReply.None)]
    [InlineData(RouterReply.Waiting)]
    [InlineData(RouterReply.Answering)]
    [InlineData(RouterReply.NotAnswering)]
    public void Describe_WithoutAnOutage_SaysNothing(RouterReply reply)
    {
        Assert.Null(RouterCheck.Describe(reply, hasAnswered: true, isOutage: false));
        Assert.Null(RouterCheck.Describe(reply, hasAnswered: false, isOutage: false));
    }

    [Fact]
    public void Summarize_WithoutARouter_IsNone()
    {
        Assert.Equal(RouterReply.None, RouterCheck.Summarize(Array.Empty<bool?>()));
    }

    [Fact]
    public void Summarize_OneOfTheRoutersAddressesAnswering_IsEnough()
    {
        // Like a router that answers on its IPv4 address but not its IPv6 one.
        Assert.Equal(RouterReply.Answering, RouterCheck.Summarize(new bool?[] { false, true }));
        Assert.Equal(RouterReply.Answering, RouterCheck.Summarize(new bool?[] { null, true }));
    }

    [Fact]
    public void Summarize_WhileAPingHasntFinished_IsWaiting()
    {
        Assert.Equal(RouterReply.Waiting, RouterCheck.Summarize(new bool?[] { null }));
        Assert.Equal(RouterReply.Waiting, RouterCheck.Summarize(new bool?[] { false, null }));
    }

    [Fact]
    public void Summarize_NoneAnswering_IsNotAnswering()
    {
        Assert.Equal(RouterReply.NotAnswering, RouterCheck.Summarize(new bool?[] { false, false }));
    }

    [Fact]
    public void GetRoutersToCheck_WhenPingingTheInternet_ChecksEveryRouter()
    {
        var routers = new[] { Home, HomeIPv6 };

        Assert.Equal(routers, RouterCheck.GetRoutersToCheck(routers, Internet));
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("fe80::1")] // The router's IPv6 address, typed without its zone.
    public void GetRoutersToCheck_WhenPingingTheRouter_ChecksNone(string pinged)
    {
        // Its other address is the same router, so pinging that wouldn't say anything the reading doesn't.
        Assert.Empty(RouterCheck.GetRoutersToCheck(new[] { Home, HomeIPv6 }, IPAddress.Parse(pinged)));
    }

    [Fact]
    public void ChooseRouters_OnWiFi_ChecksItsRouter()
    {
        var routers = ChooseRouters(WiFi, WiFi, HyperV);

        Assert.Equal(new[] { "192.168.1.1", "fe80::1%12" }, routers.Select(x => x.Network.Gateway));
        Assert.All(routers, x => Assert.Equal("wifi", x.InternetAdapterId));
    }

    [Fact]
    public void ChooseRouters_OnAnExternalVirtualSwitch_ChecksItsRouter()
    {
        // Hyper-V's external switch puts the PC's own connection on a virtual adapter, which isn't a VPN.
        Assert.Equal(new[] { "192.168.0.1" }, ChooseRouters(ExternalSwitch, ExternalSwitch, HyperV).Select(x => x.Network.Gateway));
    }

    [Fact]
    public void ChooseRouters_ThroughAVpnWithoutAGateway_ChecksTheRouterItRunsOver()
    {
        var routers = ChooseRouters(WireGuard, WiFi, WireGuard, HyperV);

        Assert.Equal(new[] { "192.168.1.1", "fe80::1%12" }, routers.Select(x => x.Network.Gateway));
        Assert.All(routers, x => Assert.Equal("wifi", x.Network.AdapterId));

        // Remembered apart from the same router without the VPN, which may keep the PC from reaching it.
        Assert.All(routers, x => Assert.Equal("wireguard", x.InternetAdapterId));
        Assert.False(routers[0].Matches(Home));
    }

    [Fact]
    public void ChooseRouters_ThroughAVpnWithAGateway_ChecksTheRouterItRunsOverRatherThanTheVpnsServer()
    {
        Assert.Equal(new[] { "10.0.0.1" }, ChooseRouters(OpenVpn, OpenVpn, Ethernet).Select(x => x.Network.Gateway));
        Assert.Equal(new[] { "10.0.0.1" }, ChooseRouters(WindowsVpn, WindowsVpn, Ethernet).Select(x => x.Network.Gateway));
    }

    [Fact]
    public void ChooseRouters_ThroughAVpnOverAVpn_SkipsTheOtherVpnsServer()
    {
        Assert.Equal(new[] { "10.0.0.1" }, ChooseRouters(WireGuard, WireGuard, OpenVpn, Ethernet).Select(x => x.Network.Gateway));
    }

    [Fact]
    public void ChooseRouters_ThroughAVpnWithEthernetAndWiFiOnTheSameNetwork_ChecksTheirRouter()
    {
        Assert.Equal(new[] { "192.168.1.1", "fe80::1%12", "192.168.1.1" }, ChooseRouters(WireGuard, WireGuard, WiFi, Dock).Select(x => x.Network.Gateway));
    }

    [Fact]
    public void ChooseRouters_ThroughAVpnThatCouldRunOverEitherOfTwoNetworks_ChecksNone()
    {
        // Like Ethernet and a phone's hotspot over Wi-Fi: one router answering wouldn't say the one the VPN runs over is fine.
        Assert.Empty(ChooseRouters(WireGuard, WireGuard, WiFi, Ethernet));
    }

    [Fact]
    public void ChooseRouters_WithoutARouteToTheInternet_ChecksNone()
    {
        Assert.Empty(ChooseRouters(null, WiFi, Ethernet));
    }

    [Fact]
    public void ChooseRouters_WithNoRouterAnywhere_ChecksNone()
    {
        // Like a VPN over mobile broadband that doesn't report a gateway.
        Assert.Empty(ChooseRouters(WireGuard, WireGuard, HyperV));
    }

    [Fact]
    public void Check_AfterTheRouterAnswers_SaysItsAnswering()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home }, Internet);
        Assert.Equal((RouterReply.Waiting, false), check.GetState());

        pings.Answer(Home, true);
        Assert.Equal((RouterReply.Answering, true), check.GetState());
    }

    [Fact]
    public void Check_WhenARouterThatHasNeverAnsweredDoesnt_SaysItHasntAnswered()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, false);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Fact]
    public void Check_SendsOnePingAtATimeToEachRouter()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        // A router that doesn't answer takes the whole timeout to say so, over several ticks.
        check.Check(new[] { Home, HomeIPv6 }, Internet);
        check.Check(new[] { Home, HomeIPv6 }, Internet);
        check.Check(new[] { Home, HomeIPv6 }, Internet);
        Assert.Equal(2, pings.Sent.Count);

        pings.Answer(Home, false);
        check.Check(new[] { Home, HomeIPv6 }, Internet);
        Assert.Equal(3, pings.Sent.Count);
        Assert.Equal(new[] { Home.Address, HomeIPv6.Address, Home.Address }, pings.Sent.Select(x => x.Address));
    }

    [Fact]
    public void Check_WhenPingingTheRouter_SendsNothing()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home }, Home.Address);

        Assert.Empty(pings.Sent);
        Assert.Equal(RouterReply.None, check.GetState().Reply);
    }

    [Fact]
    public void Check_APingThatCantBeSent_IsNoAnswer()
    {
        var check = new RouterCheck(_ => throw new PingException("No route"));

        check.Check(new[] { Home }, Internet);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Fact]
    public void Stop_StartsTheNextOutageFresh_ButRemembersTheRouterAnswered()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        // A ping or two lost on a healthy connection, while the router answers.
        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, true);
        check.Stop();

        // Later, it stops answering.
        check.Check(new[] { Home }, Internet);
        Assert.Equal((RouterReply.Waiting, true), check.GetState());

        pings.Answer(Home, false);
        Assert.Equal((RouterReply.NotAnswering, true), check.GetState());
    }

    [Fact]
    public void Stop_APingFromBeforeTheInternetCameBack_DoesntCountTowardTheNextOutage()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home }, Internet);
        check.Stop();
        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, false);

        Assert.Equal(RouterReply.Waiting, check.GetState().Reply);

        // The next tick sends a ping of its own.
        check.Check(new[] { Home }, Internet);
        Assert.Equal(2, pings.Sent.Count);
    }

    [Fact]
    public void Stop_StillRemembersARouterThatAnsweredAfterward()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home }, Internet);
        check.Stop();
        pings.Answer(Home, true);

        check.Check(new[] { Home }, Internet);
        Assert.True(check.GetState().HasAnswered);
    }

    [Fact]
    public void GetState_ARouterWithTheSameAddressOnAnotherNetwork_HasntAnswered()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);
        var hotel = new Router(Home.Address, new NetworkId("wifi", "192.168.1.1", "DD-EE-FF-33-44-55"), "wifi");

        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, true);
        check.Stop();

        check.Check(new[] { hotel }, Internet);
        pings.Answer(hotel, false);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Fact]
    public void GetState_OnceTheRoutersHardwareAddressIsKnown_DoesntCountAnotherRouterWithTheSameAddress()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);
        var beforeLookup = new Router(Home.Address, new NetworkId("wifi", "192.168.1.1", null), "wifi");
        var hotel = new Router(Home.Address, new NetworkId("wifi", "192.168.1.1", "DD-EE-FF-33-44-55"), "wifi");

        // It answered before Windows had looked up its hardware address, and again after.
        check.Check(new[] { beforeLookup }, Internet);
        pings.Answer(beforeLookup, true);
        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, true);
        check.Stop();

        check.Check(new[] { hotel }, Internet);
        pings.Answer(hotel, false);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // A network without IPv4, where fe80::1 is all there is to go by.
    public void GetState_ARouterWithTheSameIPv6AddressOnAnotherNetwork_HasntAnswered(bool homeHasIPv4)
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);
        var home = homeHasIPv4 ? new[] { Home, HomeIPv6 } : new[] { HomeIPv6 };
        var cafe = new Router(IPAddress.Parse("10.10.0.1"), new NetworkId("wifi", "10.10.0.1", "DD-EE-FF-33-44-55"), "wifi");
        var cafeIPv6 = new Router(HomeIPv6.Address, new NetworkId("wifi", "fe80::1%12", null), "wifi");

        check.Check(home, Internet);

        foreach (var router in home)
            pings.Answer(router, true);

        check.Stop();

        // Many routers use fe80::1, so it's the IPv4 address and its hardware address that say which router this is.
        check.Check(new[] { cafe, cafeIPv6 }, Internet);
        pings.Answer(cafe, false);
        pings.Answer(cafeIPv6, false);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Fact]
    public void GetState_ARouterThatOnlyAnswersOnIPv6_HasAnswered()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home, HomeIPv6 }, Internet);
        pings.Answer(Home, false);
        pings.Answer(HomeIPv6, true);
        check.Stop();

        check.Check(new[] { Home, HomeIPv6 }, Internet);
        pings.Answer(Home, false);
        pings.Answer(HomeIPv6, false);

        Assert.Equal((RouterReply.NotAnswering, true), check.GetState());
    }

    [Fact]
    public void GetState_OnANetworkWithoutIPv4_GoesByTheRoutersIPv6Address()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { HomeIPv6 }, Internet);
        pings.Answer(HomeIPv6, true);
        check.Stop();

        check.Check(new[] { HomeIPv6 }, Internet);
        pings.Answer(HomeIPv6, false);

        Assert.Equal((RouterReply.NotAnswering, true), check.GetState());
    }

    [Fact]
    public void GetState_OnANetworkWithoutIPv4_DoesntCountAnotherNetworksRouterWithTheSameIPv6Address()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);
        var ipv6Only = new Router(HomeIPv6.Address, new NetworkId("wifi", "fe80::1%12", null), "wifi");

        check.Check(new[] { Home, HomeIPv6 }, Internet);
        pings.Answer(Home, true);
        pings.Answer(HomeIPv6, true);
        check.Stop();

        // Without an IPv4 address, it can't be told from any other router that uses fe80::1.
        check.Check(new[] { ipv6Only }, Internet);
        pings.Answer(ipv6Only, false);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Fact]
    public void GetState_AfterConnectingAVpn_DoesntCountTheRouterAnsweringWithoutIt()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);
        var throughVpn = new Router(Home.Address, Home.Network, "wireguard");

        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, true);
        check.Stop();

        // A VPN can block the PC from reaching its router, so not answering through one isn't blamed on the router until it has answered through it too.
        check.Check(new[] { throughVpn }, Internet);
        pings.Answer(throughVpn, false);

        Assert.Equal((RouterReply.NotAnswering, false), check.GetState());
    }

    [Fact]
    public void GetState_OneAddressOfTheRouterAnswering_IsAnswering()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);

        check.Check(new[] { Home, HomeIPv6 }, Internet);
        pings.Answer(HomeIPv6, false);
        Assert.Equal(RouterReply.Waiting, check.GetState().Reply);

        pings.Answer(Home, true);
        Assert.Equal((RouterReply.Answering, true), check.GetState());
    }

    [Fact]
    public void GetState_ForgetsARouterThatsGone()
    {
        var pings = new FakePings();
        var check = new RouterCheck(pings.Send);
        var ethernet = new Router(IPAddress.Parse("10.0.0.1"), new NetworkId("ethernet", "10.0.0.1", null), "ethernet");

        check.Check(new[] { Home }, Internet);
        pings.Answer(Home, true);

        // Moving to another network in the middle of an outage.
        check.Check(new[] { ethernet }, Internet);

        Assert.Equal((RouterReply.Waiting, false), check.GetState());
    }

    private static (IPAddress, NetworkId) Gateway(string adapterId, string address, string hardwareAddress = null) =>
        (IPAddress.Parse(address), new NetworkId(adapterId, address, hardwareAddress));

    private static Router[] ChooseRouters(FakeAdapter internetAdapter, params FakeAdapter[] adapters) =>
        RouterCheck.ChooseRouters(adapters, internetAdapter, x => Gateways.TryGetValue(x.Id, out var found) ? found : Array.Empty<(IPAddress, NetworkId)>());

    /// <summary>
    /// Pings that are answered when the test says so.
    /// </summary>
    private sealed class FakePings
    {
        public List<(IPAddress Address, TaskCompletionSource<bool> Reply)> Sent { get; } = new();

        public Task<bool> Send(IPAddress address)
        {
            var reply = new TaskCompletionSource<bool>();
            Sent.Add((address, reply));
            return reply.Task;
        }

        /// <summary>
        /// Answers the ping to <paramref name="router" /> that's still out, and returns once the reply has been noted.
        /// </summary>
        /// <remarks>
        /// It's answered on a thread of its own, as a real reply is, since one answered on the test's thread is noted later, on another.
        /// </remarks>
        public void Answer(Router router, bool answered)
        {
            var reply = Sent.Single(x => x.Address.Equals(router.Address) && !x.Reply.Task.IsCompleted).Reply;
            var thread = new Thread(() => reply.SetResult(answered));

            thread.Start();
            thread.Join();
        }
    }

    private sealed class FakeAdapter : NetworkInterface
    {
        public FakeAdapter(string id, NetworkInterfaceType type, string description)
        {
            Id = id;
            NetworkInterfaceType = type;
            Description = description;
        }

        public override string Id { get; }

        public override NetworkInterfaceType NetworkInterfaceType { get; }

        public override string Description { get; }
    }
}
