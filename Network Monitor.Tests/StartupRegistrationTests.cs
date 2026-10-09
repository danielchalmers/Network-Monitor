using System;
using System.IO;

namespace Network_Monitor.Tests;

public class StartupRegistrationTests
{
    private const string ExePath = @"C:\Users\you\AppData\Local\Network Monitor\Network Monitor.exe";

    [Theory]
    [InlineData(@"""C:\Users\you\AppData\Local\Network Monitor\Network Monitor.exe""", ExePath)]
    [InlineData(@"C:\Users\you\AppData\Local\Network Monitor\Network Monitor.exe", ExePath)]
    [InlineData(@"""C:\Apps\Network.Monitor.exe"" --minimized", @"C:\Apps\Network.Monitor.exe")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void GetPath_ReadsQuotedAndUnquotedValues(string runValue, string expected)
    {
        Assert.Equal(expected, StartupRegistration.GetPath(runValue));
    }

    [Theory]
    [InlineData(@"""C:\Users\you\AppData\Local\Network Monitor\Network Monitor.exe""", true)]
    [InlineData(@"c:\users\you\appdata\local\network monitor\network monitor.exe", true)]
    [InlineData(@"""C:\Users\you\Downloads\Network.Monitor.exe""", false)]
    [InlineData(null, false)]
    public void IsSamePath_MatchesThisExeOnly(string runValue, bool expected)
    {
        Assert.Equal(expected, StartupRegistration.IsSamePath(runValue, ExePath));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[] { 2, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 6, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 3, 0, 0, 0, 1, 2 }, false)]
    [InlineData(new byte[] { 7, 0, 0, 0 }, false)]
    public void IsApproved_ReadsTaskManagersStartupSetting(byte[] value, bool expected)
    {
        Assert.Equal(expected, StartupRegistration.IsApproved(value));
    }

    [Fact]
    public void WasDeleted_ForAnExeDeletedFromAFolderThatsStillThere_IsTrue()
    {
        var folder = Path.Combine(Path.GetTempPath(), "NetworkMonitorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            Assert.True(StartupRegistration.WasDeleted(Path.Combine(folder, "Network.Monitor.exe")));
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void WasDeleted_ForAnExeThatsStillThere_IsFalse()
    {
        var path = Path.GetTempFileName();

        try
        {
            Assert.False(StartupRegistration.WasDeleted(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(@"C:\NetworkMonitorTests\Uninstalled\Network Monitor.exe")]
    [InlineData(@"\\nas\tools\Network.Monitor.exe")]
    [InlineData("Network.Monitor.exe")]
    [InlineData(null)]
    public void WasDeleted_ForAnUninstalledCopyOrOneThatMightBeUnreachable_IsFalse(string path)
    {
        Assert.False(StartupRegistration.WasDeleted(path));
    }
}
