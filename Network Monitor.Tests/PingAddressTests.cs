using System.Net;
using Network_Monitor.Monitors;

namespace Network_Monitor.Tests;

public class PingAddressTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.138")]
    [InlineData("127.0.0.1")]
    [InlineData("1.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("223.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.254")]
    public void TryParse_TakesAPlainIPv4Address(string text)
    {
        Assert.True(PingAddress.TryParse(text, out var address));
        Assert.Equal(IPAddress.Parse(text), address);
    }

    [Theory]
    [InlineData("2001:4860:4860::8888", "2001:4860:4860:0:0:0:0:8888")]
    [InlineData("2001:4860:4860:0:0:0:0:8888", "2001:4860:4860:0:0:0:0:8888")]
    [InlineData("2001:4860:4860:0000:0000:0000:0000:8888", "2001:4860:4860:0:0:0:0:8888")]
    [InlineData("2606:4700:4700::1111", "2606:4700:4700:0:0:0:0:1111")]
    [InlineData("2001:DB8::A", "2001:db8:0:0:0:0:0:a")]
    [InlineData("::1", "0:0:0:0:0:0:0:1")]
    [InlineData("1::", "1:0:0:0:0:0:0:0")]
    [InlineData("fe80::1", "fe80:0:0:0:0:0:0:1")]
    [InlineData("fd00::1:2", "fd00:0:0:0:0:0:1:2")]
    [InlineData("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8")]
    [InlineData("1:2:3:4:5:6::8", "1:2:3:4:5:6:0:8")]
    [InlineData("::2:3:4:5:6:7:8", "0:2:3:4:5:6:7:8")]
    [InlineData("1:2:3:4:5:6:7::", "1:2:3:4:5:6:7:0")]
    [InlineData("64:ff9b::8.8.8.8", "64:ff9b:0:0:0:0:808:808")]
    [InlineData("1:2:3:4:5:6:8.8.8.8", "1:2:3:4:5:6:808:808")]
    public void TryParse_TakesAnIPv6Address(string text, string written)
    {
        Assert.True(PingAddress.TryParse(text, out var address));
        Assert.Equal(IPAddress.Parse(written), address);
        Assert.Equal(0, address.ScopeId);
    }

    [Theory]
    [InlineData(" 8.8.8.8")]
    [InlineData("8.8.8.8 ")]
    [InlineData("\t8.8.8.8\r\n")]
    [InlineData(" 8.8.8.8 ")] // Like one pasted from a web page.
    public void TryParse_IgnoresSpacesAround(string text)
    {
        Assert.True(PingAddress.TryParse(text, out var address));
        Assert.Equal(IPAddress.Parse("8.8.8.8"), address);
    }

    [Fact]
    public void TryParse_IgnoresSpacesAroundAnIPv6Address()
    {
        Assert.True(PingAddress.TryParse("  2001:4860:4860::8888  ", out var address));
        Assert.Equal(IPAddress.Parse("2001:4860:4860::8888"), address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_WithNothingTyped_IsFalse(string text)
    {
        Assert.False(PingAddress.TryParse(text, out var address));
        Assert.Null(address);
    }

    [Theory]
    [InlineData("google.com")]
    [InlineData("dns.google")]
    [InlineData("localhost")]
    [InlineData("router")]
    [InlineData("one.one.one.one")]
    [InlineData("8.8.8.8.in-addr.arpa")]
    [InlineData("xn--bcher-kva.example")]
    [InlineData("http://8.8.8.8")]
    [InlineData("\\\\8.8.8.8")]
    public void TryParse_RejectsNames(string text)
    {
        Assert.False(PingAddress.TryParse(text, out var address));
        Assert.Null(address);
    }

    [Theory]
    [InlineData("1")] // IPAddress.TryParse takes this as 0.0.0.1.
    [InlineData("8")]
    [InlineData("134744072")] // 8.8.8.8 as one number.
    [InlineData("1.2")]
    [InlineData("1.2.3")] // 1.2.0.3.
    [InlineData("1.2.3.4.5")]
    [InlineData("0x7f.1")] // 127.0.0.1.
    [InlineData("0x08.8.8.8")]
    [InlineData("0X8.8.8.8")]
    [InlineData("010.1.1.1")] // Octal, so 8.1.1.1.
    [InlineData("08.8.8.8")]
    [InlineData("8.8.8.08")]
    [InlineData("8.8.08.8")]
    [InlineData("00.1.1.1")]
    [InlineData("0008.8.8.8")]
    [InlineData("256.1.1.1")]
    [InlineData("8.8.8.256")]
    [InlineData("999.8.8.8")]
    [InlineData("-1.8.8.8")]
    [InlineData("+8.8.8.8")]
    [InlineData("8.8.8.-8")]
    [InlineData("8..8.8")]
    [InlineData(".8.8.8.8")]
    [InlineData("8.8.8.8.")]
    [InlineData("8.8.8.")]
    [InlineData("8 .8.8.8")]
    [InlineData("8.8. 8.8")]
    [InlineData("8.8.8.8x")]
    [InlineData("8,8,8,8")]
    [InlineData("8.8.8.8/32")]
    [InlineData("٨.٨.٨.٨")] // Arabic-Indic digits, which char.IsDigit takes.
    [InlineData("８.８.８.８")] // Full-width digits.
    public void TryParse_RejectsIPv4AddressesThatArentWrittenOutInFull(string text)
    {
        Assert.False(PingAddress.TryParse(text, out var address));
        Assert.Null(address);
    }

    [Theory]
    [InlineData("8.8.8.8:53")]
    [InlineData("[2001:4860:4860::8888]")]
    [InlineData("[2001:4860:4860::8888]:53")]
    [InlineData("fe80::1%12")] // A zone, which picks an adapter.
    [InlineData("fe80::1%eth0")]
    [InlineData("fe80::1%")]
    [InlineData(":")]
    [InlineData(":::")]
    [InlineData("1:::2")]
    [InlineData("1::2::3")]
    [InlineData("::1::")]
    [InlineData(":1:2:3:4:5:6:7")]
    [InlineData("1:2:3:4:5:6:7:")]
    [InlineData(":1:2:3:4:5:6:7:8")]
    [InlineData("1:2:3:4:5:6:7")] // Seven groups with nothing left out.
    [InlineData("1:2:3:4:5:6:7:8:9")]
    [InlineData("::1:2:3:4:5:6:7:8")] // "::" has to leave out at least one group.
    [InlineData("1:2:3:4::5:6:7:8")]
    [InlineData("1:2:3:4:5:6:7:8::")]
    [InlineData("12345::1")]
    [InlineData("2001:db8::g")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:00001")]
    [InlineData("0x2001::1")]
    [InlineData("2001: db8::1")]
    [InlineData("2001:db8 ::1")]
    [InlineData("1:2:3:4:5:6:7:8.8.8.8")] // Nine groups with the IPv4 part.
    [InlineData("::8.8.8")]
    [InlineData("::8.8.8.256")]
    [InlineData("8.8.8.8::")] // An IPv4 part only goes at the end.
    [InlineData("8.8.8.8::1")]
    [InlineData("::8.8.8.8:1")]
    [InlineData("::8.8.8.8.8")]
    [InlineData("::١")]
    [InlineData("::ａ")] // A full-width "a".
    public void TryParse_RejectsWhatIsntAnIPv6Address(string text)
    {
        Assert.False(PingAddress.TryParse(text, out var address));
        Assert.Null(address);
    }

    [Theory]
    [InlineData("0.0.0.0")] // No address at all.
    [InlineData("0.1.2.3")] // This network.
    [InlineData("255.255.255.255")] // Every device on the network.
    [InlineData("224.0.0.1")] // Multicast, from 224 to 239.
    [InlineData("224.0.0.251")]
    [InlineData("239.255.255.250")]
    [InlineData("::")]
    [InlineData("0:0:0:0:0:0:0:0")]
    [InlineData("::0.0.0.0")]
    [InlineData("ff02::1")]
    [InlineData("FF05::1:3")]
    [InlineData("ff0e::101")]
    public void TryParse_RejectsAddressesThatArentOneDevice(string text)
    {
        // Written correctly, so IPAddress.TryParse takes them too.
        Assert.True(IPAddress.TryParse(text, out _));

        Assert.False(PingAddress.TryParse(text, out var address));
        Assert.Null(address);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.2.3")]
    [InlineData("0x7f.1")]
    [InlineData("010.1.1.1")]
    [InlineData("fe80::1%12")]
    public void TryParse_RejectsWhatIPAddressTryParseTakes(string text)
    {
        Assert.True(IPAddress.TryParse(text, out _));
        Assert.False(PingAddress.TryParse(text, out _));
    }
}
