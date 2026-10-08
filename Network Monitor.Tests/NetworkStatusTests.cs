using System.Net;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class NetworkStatusTests
{
    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("fe80::1%19", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::", false)]
    public void IsRealGateway_IgnoresPlaceholders(string address, bool expected)
    {
        Assert.Equal(expected, NetworkStatus.IsRealGateway(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsRealGateway_WithoutAnAddress_IsFalse()
    {
        Assert.False(NetworkStatus.IsRealGateway(null));
    }
}
