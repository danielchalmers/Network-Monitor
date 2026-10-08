using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Network_Monitor.Tests;

public sealed class UserConfigImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetworkMonitorTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CurrentCopyPath => Path.Combine(_root, "Network.Monitor_(1).exe_Url_new", "4.1.0.0", "user.config");

    private string WriteConfig(string copyFolder, string version, DateTime writtenUtc, bool declared = true, bool withPlacement = true, string size = "96")
    {
        var path = Path.Combine(_root, copyFolder, version, "user.config");
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        var declaration = declared
            ? """
                  <configSections>
                      <sectionGroup name="userSettings" type="System.Configuration.UserSettingsGroup, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" >
                          <section name="Network_Monitor.Properties.Settings" type="System.Configuration.ClientSettingsSection, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" allowExeDefinition="MachineToLocalUser" requirePermission="false" />
                      </sectionGroup>
                  </configSections>
              """
            : "";
        var placement = withPlacement
            ? """<setting name="Placement" serializeAs="Xml"><value><WindowPlacement><Length>44</Length></WindowPlacement></value></setting>"""
            : "";

        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
            {declaration}
                <userSettings>
                    <Network_Monitor.Properties.Settings>
                        <setting name="MustUpgrade" serializeAs="String"><value>False</value></setting>
                        <setting name="Size" serializeAs="String"><value>{size}</value></setting>
                        {placement}
                    </Network_Monitor.Properties.Settings>
                </userSettings>
            </configuration>
            """);
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }

    private static string ReadSetting(string path, string name) =>
        XDocument.Load(path).Descendants("setting").FirstOrDefault(x => (string)x.Attribute("name") == name)?.Element("value")?.Value;

    [Fact]
    public void ImportIfNewCopy_CopiesTheMostRecentlySavedSettings()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.0.0.0", new DateTime(2026, 1, 1), size: "150");
        var newest = WriteConfig("Network_Monitor.exe_Url_msi", "4.0.0.0", new DateTime(2026, 9, 1), size: "200");

        var imported = UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: true);

        Assert.Equal(newest, imported);
        Assert.Equal("200", ReadSetting(CurrentCopyPath, "Size"));
    }

    [Fact]
    public void ImportIfNewCopy_WhenThisCopyHasSavedBefore_LeavesItToUpgrade()
    {
        WriteConfig("Network.Monitor_(1).exe_Url_new", "4.0.0.0", new DateTime(2026, 1, 1), size: "150");
        WriteConfig("Network_Monitor.exe_Url_msi", "4.0.0.0", new DateTime(2026, 9, 1), size: "200");

        var imported = UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: true);

        Assert.Null(imported);
        Assert.False(File.Exists(CurrentCopyPath));
    }

    [Fact]
    public void ImportIfNewCopy_SkipsFilesThatCantBeRead()
    {
        var readable = WriteConfig("Network.Monitor.exe_Url_old", "4.0.0.0", new DateTime(2026, 1, 1), size: "150");
        var truncated = Path.Combine(_root, "Network_Monitor.exe_Url_msi", "4.0.0.0", "user.config");
        Directory.CreateDirectory(Path.GetDirectoryName(truncated));
        File.WriteAllText(truncated, "<?xml");
        File.SetLastWriteTimeUtc(truncated, new DateTime(2026, 9, 1));

        Assert.Equal(readable, UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: true));
    }

    [Fact]
    public void ImportIfNewCopy_AddsTheSectionDeclarationWhenItsMissing()
    {
        WriteConfig("Network_Monitor.exe_Url_debug", "4.1.0.0", new DateTime(2026, 9, 1), declared: false);

        UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: true);

        var section = XDocument.Load(CurrentCopyPath).Root.Element("configSections")?.Element("sectionGroup")?.Element("section");
        Assert.Equal("Network_Monitor.Properties.Settings", (string)section?.Attribute("name"));
    }

    [Fact]
    public void ImportIfNewCopy_CanLeaveThePositionBehind()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.0.0.0", new DateTime(2026, 1, 1));

        UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: false);

        Assert.Null(ReadSetting(CurrentCopyPath, "Placement"));
        Assert.Equal("96", ReadSetting(CurrentCopyPath, "Size"));
    }

    [Fact]
    public void ImportIfNewCopy_LeavesTheOtherCopiesFilesUntouched()
    {
        var source = WriteConfig("Network.Monitor.exe_Url_old", "4.0.0.0", new DateTime(2026, 1, 1), declared: false);
        var before = File.ReadAllText(source);

        UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: false);

        Assert.Equal(before, File.ReadAllText(source));
        Assert.Equal(new DateTime(2026, 1, 1), File.GetLastWriteTimeUtc(source));
    }

    [Fact]
    public void ImportIfNewCopy_WithNothingToImport_StartsFromDefaults()
    {
        Assert.Null(UserConfigImport.ImportIfNewCopy(CurrentCopyPath, includePlacement: true));
        Assert.False(File.Exists(CurrentCopyPath));
    }

    [Theory]
    [InlineData(@"C:\Settings\Copy\4.1.0.0\user.config", @"C:\Settings\Copy", true)]
    [InlineData(@"C:\Settings\Copy\4.1.0.0\user.config", @"C:\Settings\Copy\", true)]
    [InlineData(@"C:\Settings\CopyTwo\4.1.0.0\user.config", @"C:\Settings\Copy", false)]
    [InlineData(@"C:\Settings\Other\user.config", @"C:\Settings\Copy", false)]
    public void IsInFolder_ShouldOnlyMatchThatFolder(string path, string folder, bool expected)
    {
        Assert.Equal(expected, SettingsStartup.IsInFolder(path, folder));
    }
}
