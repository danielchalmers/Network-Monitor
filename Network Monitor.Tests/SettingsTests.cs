using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Threading;
using Network_Monitor.Properties;
using WpfWindowPlacement;

namespace Network_Monitor.Tests;

public sealed class SettingsTests : IDisposable
{
    private static readonly WindowPlacement SomePlacement = new()
    {
        Length = 44,
        Flags = WindowPlacementFlags.SetMinimizedPosition | WindowPlacementFlags.RestoreToMaximized,
        ShowCommand = WindowPlacementShowCommand.ShowNoActivate,
        MinimizedPosition = new(-1, -1),
        MaximizedPosition = new(-8, -8),
        NormalBounds = new(1200, 600, 1296, 712),
    };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NetworkMonitorTests", Guid.NewGuid().ToString("N"));

    public SettingsTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private string FilePath => Path.Combine(_folder, "Network-Monitor-x64.settings");

    /// <summary>
    /// Loads the settings with no 4.x settings to bring in, so they start from the defaults.
    /// </summary>
    private Settings Load() => Settings.Load(FilePath, Path.Combine(_folder, "No 4.x settings"));

    [Fact]
    public void Save_ThenLoad_KeepsEverySetting()
    {
        var original = Load();
        original.PingHost = "2606:4700:4700::1111";
        original.Bits = false;
        original.InterfaceId = "{4D36E972-E325-11CE-BFC1-08002BE10318}";
        original.Topmost = false;
        original.HideWhenFullscreen = false;
        original.ShowInTaskbar = false;
        original.Horizontal = true;
        original.Theme = AppTheme.Dark;
        original.Size = 150;
        original.Placement = SomePlacement;

        Assert.True(original.Save());

        var loaded = Load();
        Assert.Equal("2606:4700:4700::1111", loaded.PingHost);
        Assert.False(loaded.Bits);
        Assert.Equal("{4D36E972-E325-11CE-BFC1-08002BE10318}", loaded.InterfaceId);
        Assert.False(loaded.Topmost);
        Assert.False(loaded.HideWhenFullscreen);
        Assert.False(loaded.ShowInTaskbar);
        Assert.True(loaded.Horizontal);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(150, loaded.Size);
        Assert.Equal(SomePlacement, loaded.Placement);
    }

    [Fact]
    public void Save_WritesNamesAndValuesThatCanBeReadAndChangedByHand()
    {
        var settings = Load();
        settings.Theme = AppTheme.Dark;
        settings.Placement = SomePlacement;
        Assert.True(settings.Save());

        var text = File.ReadAllText(FilePath);

        Assert.StartsWith("{" + Environment.NewLine + "  \"", text);
        Assert.Contains("\"Theme\": \"Dark\"", text);
        Assert.Contains("\"ShowCommand\":4", text);
        Assert.Contains("\"Left\":1200", text);
    }

    [Fact]
    public void Load_WithNoFile_StartsFromTheDefaultsAndCreatesIt()
    {
        var settings = Load();

        Assert.True(settings.Bits);
        Assert.Equal(string.Empty, settings.InterfaceId);
        Assert.True(settings.Topmost);
        Assert.True(settings.HideWhenFullscreen);
        Assert.True(settings.ShowInTaskbar);
        Assert.False(settings.Horizontal);
        Assert.Equal(AppTheme.Auto, settings.Theme);
        Assert.Equal(96, settings.Size);
        Assert.Equal(default, settings.Placement);
        Assert.Equal("8.8.8.8", settings.PingHost);
        Assert.Equal(TimeSpan.FromSeconds(4), settings.Timeout);
        Assert.True(settings.CanBeSaved);
        Assert.True(File.Exists(FilePath));
    }

    [Theory]
    [InlineData("")] // Like a file left empty by a shutdown in the middle of saving.
    [InlineData("not settings")]
    [InlineData("""{ "Size": 150""")] // Cut short.
    [InlineData("[]")]
    [InlineData("null")]
    public void Load_WithAFileThatCantBeRead_StartsFromTheDefaults(string contents)
    {
        File.WriteAllText(FilePath, contents);

        var settings = Load();

        Assert.Equal(96, settings.Size);
        Assert.True(settings.CanBeSaved);
        Assert.IsType<Dictionary<string, object>>(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FilePath)));
    }

    [Fact]
    public void Load_WithAFileThatCantBeRead_KeepsACopyOfIt()
    {
        const string contents = """{ "Theme": "Dark" "Size": 150 }""";
        File.WriteAllText(FilePath, contents);

        var settings = Load();

        Assert.Equal(AppTheme.Auto, settings.Theme);
        Assert.True(settings.CanBeSaved);
        Assert.Equal(contents, File.ReadAllText(FilePath + ".bad"));
    }

    [Fact]
    public void Load_WithAnEmptyFile_KeepsNoCopy()
    {
        File.WriteAllText(FilePath, "");

        Load();

        Assert.False(File.Exists(FilePath + ".bad"));
    }

    [Fact]
    public void Load_WithSomeBadValues_KeepsTheGoodOnes()
    {
        File.WriteAllText(FilePath, """
            {
              "PingHost": "google.com",
              "Bits": "maybe",
              "InterfaceId": 7,
              "Topmost": false,
              "ShowInTaskbar": null,
              "Horizontal": true,
              "Theme": "Purple",
              "Size": 12.5,
              "Placement": { "Length": "long", "NormalBounds": { "Left": 1 } }
            }
            """);

        var settings = Load();

        Assert.Equal("8.8.8.8", settings.PingHost);
        Assert.True(settings.Bits);
        Assert.Equal(string.Empty, settings.InterfaceId);
        Assert.False(settings.Topmost);
        Assert.True(settings.ShowInTaskbar);
        Assert.True(settings.Horizontal);
        Assert.Equal(AppTheme.Auto, settings.Theme);
        Assert.Equal(96, settings.Size);
        Assert.Equal(default, settings.Placement);
    }

    [Fact]
    public void Load_AFileSavedBeforeHideWhenFullscreen_TurnsItOn()
    {
        // It's on by default, for a copy that's been running without it too.
        File.WriteAllText(FilePath, """{ "Topmost": false }""");

        Assert.True(Load().HideWhenFullscreen);
    }

    [Fact]
    public void Load_ReadsValuesTheWayTheyMightBeWrittenByHand()
    {
        File.WriteAllText(FilePath, """
            {
              "theme": "dark",
              "Size": "150",
              "Topmost": "False",
              "Placement": { "NormalBounds": { "Left": 10, "Top": 20, "Right": 106, "Bottom": 132 }, "ShowCommand": 1 },
              "FromANewerVersion": [1, { "a": null }]
            }
            """);

        var settings = Load();

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(150, settings.Size);
        Assert.False(settings.Topmost);
        Assert.Equal(WindowPlacementShowCommand.ShowNormal, settings.Placement.ShowCommand);
        Assert.Equal(new Rectangle(10, 20, 106, 132), settings.Placement.NormalBounds);
    }

    [Theory]
    [InlineData("\"5\"")]
    [InlineData("5")]
    [InlineData("\"Light, Dark\"")]
    [InlineData("\"\"")]
    public void Load_WithAThemeThatIsntOneOfTheChoices_KeepsTheDefault(string theme)
    {
        File.WriteAllText(FilePath, $$"""{ "Theme": {{theme}} }""");

        Assert.Equal(AppTheme.Auto, Load().Theme);
    }

    [Theory]
    [InlineData(0, SizeScaleConverter.MinSize)]
    [InlineData(-5, SizeScaleConverter.MinSize)]
    [InlineData(5000, SizeScaleConverter.MaxSize)]
    public void Load_WithASizeOutOfRange_KeepsTheWidgetOnScreen(int size, int expected)
    {
        File.WriteAllText(FilePath, $$"""{ "Size": {{size}} }""");

        Assert.Equal(expected, Load().Size);
    }

    [Fact]
    public void Load_ReadsAPingAddressEditedByHand()
    {
        File.WriteAllText(FilePath, """{ "PingHost": " 1.1.1.1 " }""");

        Assert.Equal("1.1.1.1", Load().PingHost);
    }

    [Theory]
    [InlineData("\"one.one.one.one\"")]
    [InlineData("\"1.1.1\"")]
    [InlineData("\"255.255.255.255\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("1")]
    public void Load_WithAPingAddressThatCantBePinged_KeepsTheDefault(string pingHost)
    {
        File.WriteAllText(FilePath, $$"""{ "PingHost": {{pingHost}} }""");

        Assert.Equal("8.8.8.8", Load().PingHost);
    }

    [Fact]
    public void PingHost_GivenOneThatCantBePinged_KeepsTheAddressBefore()
    {
        var settings = Load();
        settings.PingHost = "192.168.1.1";

        settings.PingHost = "router.local";
        Assert.Equal("192.168.1.1", settings.PingHost);

        settings.PingHost = null;
        Assert.Equal("192.168.1.1", settings.PingHost);
    }

    [Fact]
    public void Save_OverAnExistingFile_LeavesNoTemporaryFileBehind()
    {
        var settings = Load();
        settings.Size = 100;
        Assert.True(settings.Save());
        settings.Size = 120;
        Assert.True(settings.Save());

        Assert.Equal(120, Load().Size);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_WhereTheFileCantBeWritten_StillStarts()
    {
        var settings = Settings.Load(Path.Combine(_folder, "Missing folder", "Network-Monitor-x64.settings"), _folder);

        Assert.Equal(96, settings.Size);
        Assert.False(settings.CanBeSaved);
    }

    [Fact]
    public void Load_WithALockedFile_DoesntSaveOverIt()
    {
        File.WriteAllText(FilePath, """{ "Size": 150 }""");

        Settings settings;
        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            settings = Load();

        Assert.False(settings.CanBeSaved);
        Assert.Equal("""{ "Size": 150 }""", File.ReadAllText(FilePath));
    }

    [Fact]
    public void ChangingASetting_SavesItAboutASecondLater()
    {
        var settings = Load();
        settings.Size = 123;

        Assert.DoesNotContain("123", File.ReadAllText(FilePath));

        PumpDispatcher(TimeSpan.FromSeconds(2));

        Assert.Contains("\"Size\": 123", File.ReadAllText(FilePath));
    }

    [Fact]
    public void SettingTheSameValue_StillAnnouncesIt()
    {
        // The theme menu items only show the setting, so clicking the checked one relies on this to check it again.
        var settings = Load();
        var announced = new List<string>();
        settings.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        settings.Theme = settings.Theme;
        settings.Save();

        Assert.Equal(new[] { nameof(Settings.Theme) }, announced);
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var stopTimer = new DispatcherTimer { Interval = duration };
        stopTimer.Tick += (_, _) =>
        {
            stopTimer.Stop();
            frame.Continue = false;
        };
        stopTimer.Start();
        Dispatcher.PushFrame(frame);
    }
}
