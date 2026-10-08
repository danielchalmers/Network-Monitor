using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class NetworkIdTests
{
    [Fact]
    public void Matches_TheSameRouterOnTheSameAdapter()
    {
        Assert.True(new NetworkId("wifi", "192.168.1.1", "AA-BB-CC-00-11-22").Matches(new NetworkId("wifi", "192.168.1.1", "aa-bb-cc-00-11-22")));
    }

    [Fact]
    public void Matches_WhenTheRoutersHardwareAddressIsntKnownYet()
    {
        Assert.True(new NetworkId("wifi", "192.168.1.1", null).Matches(new NetworkId("wifi", "192.168.1.1", "AA-BB-CC-00-11-22")));
    }

    [Fact]
    public void DoesntMatch_ADifferentRouterWithTheSameCommonAddress()
    {
        // A hotel's router can use the same 192.168.1.1 as the one at home.
        Assert.False(new NetworkId("wifi", "192.168.1.1", "AA-BB-CC-00-11-22").Matches(new NetworkId("wifi", "192.168.1.1", "DD-EE-FF-33-44-55")));
    }

    [Fact]
    public void DoesntMatch_AVpnTunnelOverTheSameRouter()
    {
        Assert.False(new NetworkId("vpn", null, null).Matches(new NetworkId("wifi", "192.168.1.1", null)));
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("fe80::1%12", false)]
    [InlineData(null, false)]
    public void IsIPv4_TellsRouterAddressesApart(string gateway, bool expected)
    {
        Assert.Equal(expected, new NetworkId("wifi", gateway, null).IsIPv4);
    }
}
