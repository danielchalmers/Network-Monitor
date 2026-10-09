using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Network_Monitor.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void GetSignalName_IgnoresCase()
    {
        Assert.Equal(
            SingleInstance.GetSignalName(@"C:\Users\Me\AppData\Local\Network Monitor\Network Monitor.exe"),
            SingleInstance.GetSignalName(@"c:\users\me\appdata\local\network monitor\NETWORK MONITOR.EXE"));
    }

    [Fact]
    public void GetSignalName_DiffersBetweenCopies()
    {
        Assert.NotEqual(
            SingleInstance.GetSignalName(@"C:\Users\Me\AppData\Local\Network Monitor\Network Monitor.exe"),
            SingleInstance.GetSignalName(@"C:\Users\Me\Downloads\Network.Monitor.exe"));
    }

    [Fact]
    public void TryClaim_WhenTheRunningCopysSignalCantBeOpened_RunsAnyway()
    {
        // A copy running as administrator leaves a signal that a normal one isn't allowed to open; denying access here fails the same way.
        var exePath = @"C:\Test\" + Guid.NewGuid() + ".exe";
        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(WindowsIdentity.GetCurrent().User, EventWaitHandleRights.FullControl, AccessControlType.Deny));

        using var elevatedCopysSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.GetSignalName(exePath), out _, security);

        Assert.True(SingleInstance.TryClaim(exePath));
        SingleInstance.Release();
    }

    [Fact]
    public void GetSignalName_IsAValidObjectName()
    {
        // Named kernel objects can't contain backslashes outside a namespace prefix, so the path is hashed.
        Assert.DoesNotContain('\\', SingleInstance.GetSignalName(@"C:\Network Monitor.exe"));
    }
}
