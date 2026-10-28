using System.Linq;
using System.Net.NetworkInformation;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class NetworkAdaptersTests
{
    private static readonly FakeAdapter WiFi = new("wifi", "Wi-Fi");
    private static readonly FakeAdapter HyperV = new("hyperv", "vEthernet (Default Switch)");
    private static readonly NetworkInterface[] Adapters = { WiFi, HyperV };

    [Fact]
    public void Select_Automatic_MeasuresTheInternetAdapterOnly()
    {
        Assert.Equal(new[] { WiFi }, NetworkAdapters.Select(Adapters, string.Empty, WiFi));
    }

    [Fact]
    public void Select_AutomaticWithoutARoute_MeasuresEverything()
    {
        Assert.Equal(Adapters, NetworkAdapters.Select(Adapters, string.Empty, null));
    }

    [Fact]
    public void Select_All_MeasuresEverything()
    {
        Assert.Equal(Adapters, NetworkAdapters.Select(Adapters, NetworkAdapters.AllAdapters, WiFi));
    }

    [Fact]
    public void Select_OneAdapter_MeasuresOnlyThatOne()
    {
        Assert.Equal(new[] { HyperV }, NetworkAdapters.Select(Adapters, "hyperv", WiFi));
        Assert.Empty(NetworkAdapters.Select(Adapters, "unplugged", WiFi));
    }

    [Theory]
    [InlineData("", "Adapter: Wi-Fi (automatic)")]
    [InlineData("*", "Adapters: all 2")]
    [InlineData("hyperv", "Adapter: vEthernet (Default Switch)")]
    [InlineData("unplugged", "Adapter: Disconnected")]
    public void Describe_SaysWhatTheReadingsCover(string interfaceId, string expected)
    {
        Assert.Equal(expected, NetworkAdapters.Describe(Adapters, interfaceId, WiFi));
    }

    [Fact]
    public void Describe_AutomaticWithoutARoute_SaysItMeasuresEverything()
    {
        Assert.Equal("Adapters: all 2 (automatic)", NetworkAdapters.Describe(Adapters, string.Empty, null));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Wireless80211, "Intel(R) Wi-Fi 6 AX201 160MHz")]
    [InlineData(NetworkInterfaceType.Ethernet, "Realtek PCIe GbE Family Controller")]
    [InlineData(NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter")] // An external virtual switch carries the PC's own connection.
    [InlineData(NetworkInterfaceType.Wwanpp, "Generic Mobile Broadband Adapter")]
    public void IsTunnel_ANetworkOfItsOwn_IsNot(NetworkInterfaceType type, string description)
    {
        Assert.False(NetworkAdapters.IsTunnel(new FakeAdapter("adapter", "Adapter", type, description)));
    }

    [Theory]
    [InlineData(NetworkInterfaceType.Ppp, "WAN Miniport (IKEv2)")] // Windows' own VPN.
    [InlineData(NetworkInterfaceType.Tunnel, "Microsoft Teredo Tunneling Adapter")]
    [InlineData((NetworkInterfaceType)53, "WireGuard Tunnel")]
    [InlineData((NetworkInterfaceType)53, "Wintun Userspace Tunnel")]
    [InlineData(NetworkInterfaceType.Ethernet, "TAP-Windows Adapter V9")]
    [InlineData(NetworkInterfaceType.Ethernet, "Cisco AnyConnect Secure Mobility Client Virtual Miniport Adapter for Windows x64")]
    [InlineData(NetworkInterfaceType.Ethernet, "PANGP Virtual Ethernet Adapter Secure")]
    [InlineData(NetworkInterfaceType.Ethernet, "Fortinet SSL VPN Virtual Ethernet Adapter")]
    public void IsTunnel_AVpn_Is(NetworkInterfaceType type, string description)
    {
        Assert.True(NetworkAdapters.IsTunnel(new FakeAdapter("adapter", "Adapter", type, description)));
    }

    [Fact]
    public void FindInternetAdapter_ReturnsOneOfTheAdaptersOrNothing()
    {
        var adapters = NetworkAdapters.GetMonitorable();
        var found = NetworkAdapters.FindInternetAdapter(adapters);

        Assert.True(found is null || adapters.Contains(found));
    }

    private sealed class FakeAdapter : NetworkInterface
    {
        public FakeAdapter(string id, string name, NetworkInterfaceType type = NetworkInterfaceType.Ethernet, string description = "")
        {
            Id = id;
            Name = name;
            NetworkInterfaceType = type;
            Description = description;
        }

        public override string Id { get; }

        public override string Name { get; }

        public override NetworkInterfaceType NetworkInterfaceType { get; }

        public override string Description { get; }
    }
}
