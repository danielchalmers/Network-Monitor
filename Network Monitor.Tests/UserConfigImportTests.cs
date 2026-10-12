using System;
using System.IO;
using Network_Monitor.Properties;
using WpfWindowPlacement;

namespace Network_Monitor.Tests;

public sealed class UserConfigImportTests : IDisposable
{
    private const string InterfaceId = "{4D36E972-E325-11CE-BFC1-08002BE10318}";

    /// <summary>
    /// A window position as 4.x saved it, with XmlSerializer.
    /// </summary>
    private const string SavedPlacement = """
        <setting name="Placement" serializeAs="Xml">
            <value>
                <WindowPlacement xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                    <Length>44</Length>
                    <Flags />
                    <ShowCommand>ShowNormal</ShowCommand>
                    <MinimizedPosition><X>-1</X><Y>-1</Y></MinimizedPosition>
                    <MaximizedPosition><X>-1</X><Y>-1</Y></MaximizedPosition>
                    <NormalBounds><Left>1200</Left><Top>600</Top><Right>1296</Right><Bottom>712</Bottom></NormalBounds>
                </WindowPlacement>
            </value>
        </setting>
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "NetworkMonitorTests", Guid.NewGuid().ToString("N"));

    public UserConfigImportTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// Where 4.x kept its settings, like %LOCALAPPDATA%\Network_Monitor.
    /// </summary>
    private string UserConfigRoot => Path.Combine(_root, "Network_Monitor");

    private string SettingsPath => Path.Combine(_root, "Downloads", "Network-Monitor-x64.settings");

    private string WriteConfig(string copyFolder, string version, DateTime writtenUtc, bool declared = true, string placement = SavedPlacement, string size = "150", string theme = "Dark")
    {
        var path = Path.Combine(UserConfigRoot, copyFolder, version, "user.config");
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // Files written by a build that came with its .exe.config, such as a debug build, leave the declaration out.
        var declaration = declared
            ? """
                  <configSections>
                      <sectionGroup name="userSettings" type="System.Configuration.UserSettingsGroup, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" >
                          <section name="Network_Monitor.Properties.Settings" type="System.Configuration.ClientSettingsSection, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" allowExeDefinition="MachineToLocalUser" requirePermission="false" />
                      </sectionGroup>
                  </configSections>
              """
            : "";

        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
            {declaration}
                <userSettings>
                    <Network_Monitor.Properties.Settings>
                        <setting name="MustUpgrade" serializeAs="String"><value>False</value></setting>
                        <setting name="Topmost" serializeAs="String"><value>False</value></setting>
                        <setting name="ShowInTaskbar" serializeAs="String"><value>False</value></setting>
                        <setting name="Size" serializeAs="String"><value>{size}</value></setting>
                        <setting name="Bits" serializeAs="String"><value>False</value></setting>
                        <setting name="Horizontal" serializeAs="String"><value>True</value></setting>
                        <setting name="Theme" serializeAs="String"><value>{theme}</value></setting>
                        <setting name="InterfaceId" serializeAs="String"><value>{InterfaceId}</value></setting>
                        {placement}
                    </Network_Monitor.Properties.Settings>
                </userSettings>
            </configuration>
            """);
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }

    /// <summary>
    /// Returns settings with their defaults, to read a user.config into.
    /// </summary>
    private Settings CreateDefaultSettings() =>
        Settings.Load(Path.Combine(_root, "Defaults.settings"), Path.Combine(_root, "No 4.x settings"));

    [Fact]
    public void Load_TheFirstTime_BringsInThe4xSettings()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 9, 1));

        var settings = Settings.Load(SettingsPath, UserConfigRoot);

        Assert.False(settings.Topmost);
        Assert.False(settings.ShowInTaskbar);
        Assert.Equal(150, settings.Size);
        Assert.False(settings.Bits);
        Assert.True(settings.Horizontal);
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(InterfaceId, settings.InterfaceId);
        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Load_TheFirstTime_UsesTheMostRecentlySavedCopysSettings()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.0.0.0", new DateTime(2026, 1, 1), size: "150");
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 6, 1), size: "175");
        WriteConfig("Network_Monitor.exe_Url_msi", "4.1.0.0", new DateTime(2026, 9, 1), size: "200");

        // This copy never ran as 4.x.
        Assert.Equal(200, Settings.Load(SettingsPath, UserConfigRoot, Path.Combine(UserConfigRoot, "Network-Monitor-x64.exe_Url_new")).Size);
    }

    [Fact]
    public void Load_TheFirstTime_PrefersThisCopysOwn4xSettings()
    {
        // The installed copy has the same path as in 4.x, so its own settings are still there, even when a portable copy saved after them.
        WriteConfig("Network_Monitor.exe_Url_msi", "4.1.0.0", new DateTime(2026, 1, 1), size: "150");
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 9, 1), size: "200");

        var settings = Settings.Load(SettingsPath, UserConfigRoot, Path.Combine(UserConfigRoot, "Network_Monitor.exe_Url_msi"));

        Assert.Equal(150, settings.Size);
        Assert.Equal(new Rectangle(1200, 600, 1296, 712), settings.Placement.NormalBounds);
    }

    [Fact]
    public void Load_OnceTheFileExists_DoesntBringThemInAgain()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 1, 1), size: "150");
        var first = Settings.Load(SettingsPath, UserConfigRoot);
        first.Size = 120;
        Assert.True(first.Save());

        // A 4.x copy that's still in use keeps saving its own settings.
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", DateTime.UtcNow, size: "250");

        Assert.Equal(120, Settings.Load(SettingsPath, UserConfigRoot).Size);
    }

    [Fact]
    public void Load_WithAFileThatCantBeRead_DoesntBringThemIn()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 1, 1), size: "150");
        File.WriteAllText(SettingsPath, "");

        Assert.Equal(96, Settings.Load(SettingsPath, UserConfigRoot).Size);
    }

    [Fact]
    public void Load_WhereTheFileCantBeWritten_StillBringsThemIn()
    {
        WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 1, 1), size: "150");

        var settings = Settings.Load(Path.Combine(_root, "Missing folder", "Network-Monitor-x64.settings"), UserConfigRoot);

        Assert.Equal(150, settings.Size);
        Assert.False(settings.CanBeSaved);
    }

    [Fact]
    public void Load_WithNothingToBringIn_StartsFromTheDefaults()
    {
        var settings = Settings.Load(SettingsPath, UserConfigRoot);

        Assert.Equal(96, settings.Size);
        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void FindNewestReadable_SkipsFilesThatCantBeRead()
    {
        var readable = WriteConfig("Network.Monitor.exe_Url_old", "4.0.0.0", new DateTime(2026, 1, 1));
        var truncated = Path.Combine(UserConfigRoot, "Network_Monitor.exe_Url_msi", "4.0.0.0", "user.config");
        Directory.CreateDirectory(Path.GetDirectoryName(truncated));
        File.WriteAllText(truncated, "<?xml");
        File.SetLastWriteTimeUtc(truncated, new DateTime(2026, 9, 1));

        Assert.Equal(readable, UserConfigImport.FindNewestReadable(UserConfigRoot));
    }

    [Fact]
    public void FindNewestReadable_WithNo4xSettings_FindsNothing()
    {
        Assert.Null(UserConfigImport.FindNewestReadable(UserConfigRoot));
    }

    [Fact]
    public void ReadInto_ReadsThePosition()
    {
        var path = WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 1, 1));
        var settings = CreateDefaultSettings();

        UserConfigImport.ReadInto(settings, path);

        Assert.Equal(new WindowPlacement
        {
            Length = 44,
            ShowCommand = WindowPlacementShowCommand.ShowNormal,
            MinimizedPosition = new(-1, -1),
            MaximizedPosition = new(-1, -1),
            NormalBounds = new(1200, 600, 1296, 712),
        }, settings.Placement);
    }

    [Fact]
    public void ReadInto_ReadsFilesWithoutTheSectionDeclaration()
    {
        var path = WriteConfig("Network_Monitor.exe_Url_debug", "0.0.0.0", new DateTime(2026, 9, 1), declared: false);
        var settings = CreateDefaultSettings();

        UserConfigImport.ReadInto(settings, path);

        Assert.Equal(150, settings.Size);
    }

    [Fact]
    public void ReadInto_SkipsValuesThatCantBeRead()
    {
        var path = WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 1, 1), size: "big", theme: "Purple",
            placement: """<setting name="Placement" serializeAs="Xml"><value><WindowPlacement><Length>long</Length></WindowPlacement></value></setting>""");
        var settings = CreateDefaultSettings();

        UserConfigImport.ReadInto(settings, path);

        Assert.Equal(96, settings.Size);
        Assert.Equal(AppTheme.Auto, settings.Theme);
        Assert.Equal(default, settings.Placement);
        Assert.True(settings.Horizontal);
    }

    [Fact]
    public void ReadInto_LeavesTheOldFileUntouched()
    {
        var path = WriteConfig("Network.Monitor.exe_Url_old", "4.1.0.0", new DateTime(2026, 1, 1));
        var before = File.ReadAllText(path);

        UserConfigImport.ReadInto(CreateDefaultSettings(), path);

        Assert.Equal(before, File.ReadAllText(path));
        Assert.Equal(new DateTime(2026, 1, 1), File.GetLastWriteTimeUtc(path));
    }
}
