using System;
using System.Globalization;
using System.Threading;

namespace Network_Monitor.Tests;

public class CrashHandlerTests
{
    [Fact]
    public void FormatLogEntry_ShouldIncludeTheTimeVersionAndException()
    {
        var time = new DateTimeOffset(2026, 10, 7, 14, 2, 31, TimeSpan.FromHours(-7));
        var exception = new InvalidOperationException("Something broke");

        var entry = CrashHandler.FormatLogEntry(exception, time);

        Assert.StartsWith("2026-10-07 14:02:31 -07:00\r\n", entry);
        Assert.Contains($"Network Monitor {App.VersionText} on ", entry);
        Assert.Contains("System.InvalidOperationException: Something broke", entry);
        Assert.EndsWith("\r\n\r\n", entry);
    }

    [Theory]
    [InlineData("th-TH")]
    [InlineData("fi-FI")]
    [InlineData("ar-SA")]
    public void FormatLogEntry_WritesTheSameTimeInEveryCulture(string cultureName)
    {
        var original = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

        try
        {
            var time = new DateTimeOffset(2026, 10, 7, 14, 2, 31, TimeSpan.FromHours(-7));

            Assert.StartsWith("2026-10-07 14:02:31 -07:00\r\n", CrashHandler.FormatLogEntry(new Exception(), time));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void VersionText_ShouldBeTheProductVersion()
    {
        // A prerelease such as "4.1.0-beta1" is fine too.
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", App.VersionText);
    }
}
