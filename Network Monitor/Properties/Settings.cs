using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using WpfWindowPlacement;

namespace Network_Monitor.Properties;

/// <summary>
/// The widget's settings, kept as JSON in a file next to the exe, as DesktopClock does, so a portable copy carries them along and each copy has its own.
/// The file is named after the exe, like "Network-Monitor-x64.settings".
/// </summary>
/// <remarks>
/// Every public property with a public setter is saved under its own name.
/// Each change is announced even when the value is the same, as the old settings did, so a menu item that only shows a setting, like the theme, shows it again after being clicked.
/// </remarks>
public sealed class Settings : ObservableObject
{
    private const int MOVEFILE_REPLACE_EXISTING = 0x1;
    private const int MOVEFILE_WRITE_THROUGH = 0x8;

    private static readonly Lazy<Settings> _default = new(LoadBesideExe);

    private static readonly PropertyInfo[] _savedProperties = typeof(Settings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(x => x.GetSetMethod() != null)
        .ToArray();

    private readonly DispatcherTimer _saveTimer;
    private readonly object _fileLock = new();

    private bool _bits = true;
    private string _interfaceId = string.Empty;
    private bool _topmost = true;
    private bool _showInTaskbar = true;
    private bool _horizontal;
    private AppTheme _theme = AppTheme.Auto;
    private int _size = 96;
    private WindowPlacement _placement;

    private Settings(string filePath)
    {
        FilePath = filePath;

        // Saving a second after the last change, rather than only on exit, means a crash, a forced restart, or a power cut doesn't lose it.
        // Waiting for changes to settle keeps a drag or a slider from writing the file over and over.
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (_, _) => Save();

        PropertyChanged += (_, _) =>
        {
            // Values being read in while loading are saved when it's done, and nothing is saved where the file can't be written.
            if (!CanBeSaved)
                return;

            _saveTimer.Stop();
            _saveTimer.Start();
        };
    }

    /// <summary>
    /// The settings of the running widget, read from the file next to the exe the first time they're needed.
    /// </summary>
    public static Settings Default => _default.Value;

    /// <summary>
    /// The full path of the settings file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Whether changes are saved to the file.
    /// False when the file couldn't be written as the settings were loaded, such as in a folder that needs admin rights.
    /// Also false when it couldn't be opened, or couldn't be read and a copy of it couldn't be kept, so the defaults aren't saved over it.
    /// </summary>
    public bool CanBeSaved { get; private set; }

    /// <summary>
    /// The address pinged to measure latency.
    /// </summary>
    /// <remarks>
    /// Not saved, so it can't be changed yet.
    /// To make it a saved setting, give it a field and a public setter that calls <c>Set(ref _pingHost, value, checkEquality: false)</c> like the others.
    /// Announcing each change is what saves it a second later and updates whatever shows it, which a plain <c>{ get; set; }</c> wouldn't do.
    /// </remarks>
    public string PingHost { get; } = "8.8.8.8";

    /// <summary>
    /// How long a ping waits for its reply.
    /// </summary>
    public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Whether speeds are shown in bits per second (Mbps) rather than bytes (MB/s).
    /// </summary>
    public bool Bits
    {
        get => _bits;
        set => Set(ref _bits, value, checkEquality: false);
    }

    /// <summary>
    /// The network adapter whose traffic is measured: empty for Automatic, which follows the adapter that reaches the internet, <see cref="Monitors.NetworkAdapters.AllAdapters" /> for all of them, or one adapter's ID.
    /// </summary>
    public string InterfaceId
    {
        get => _interfaceId;
        set => Set(ref _interfaceId, value, checkEquality: false);
    }

    /// <summary>
    /// Whether the widget stays in front of other windows.
    /// </summary>
    public bool Topmost
    {
        get => _topmost;
        set => Set(ref _topmost, value, checkEquality: false);
    }

    /// <summary>
    /// Whether the widget has a button on the taskbar.
    /// </summary>
    public bool ShowInTaskbar
    {
        get => _showInTaskbar;
        set => Set(ref _showInTaskbar, value, checkEquality: false);
    }

    /// <summary>
    /// Whether the readings sit side by side in a strip rather than stacked in a column.
    /// </summary>
    public bool Horizontal
    {
        get => _horizontal;
        set => Set(ref _horizontal, value, checkEquality: false);
    }

    /// <summary>
    /// Whether the widget is light, dark, or follows Windows.
    /// </summary>
    public AppTheme Theme
    {
        get => _theme;
        set => Set(ref _theme, value, checkEquality: false);
    }

    /// <summary>
    /// How wide the readings are, kept between <see cref="SizeScaleConverter.MinSize" /> and <see cref="SizeScaleConverter.MaxSize" />.
    /// </summary>
    public int Size
    {
        get => _size;

        // The slider and Ctrl+scroll stay in range on their own, but a size edited by hand could shrink the widget to nothing or past the edges of the screen.
        set => Set(ref _size, Math.Min(Math.Max(value, SizeScaleConverter.MinSize), SizeScaleConverter.MaxSize), checkEquality: false);
    }

    /// <summary>
    /// Where the widget was left on the screen.
    /// </summary>
    public WindowPlacement Placement
    {
        get => _placement;
        set => Set(ref _placement, value, checkEquality: false);
    }

    /// <summary>
    /// Reads the settings from <paramref name="filePath" />, or the first time, when there's no file yet, brings in another copy's, as <see cref="UserConfigImport.TryImport" /> describes.
    /// Anything that can't be read keeps its default, so a damaged file never stops the widget from starting.
    /// </summary>
    /// <param name="userConfigRoot">Where 4.x kept its settings, with a folder for each copy.</param>
    /// <param name="ownUserConfigFolder">Which of those folders is this copy's, or <c>null</c> if that isn't known.</param>
    public static Settings Load(string filePath, string userConfigRoot, string ownUserConfigFolder = null)
    {
        var settings = new Settings(filePath);
        var canOverwrite = true;

        if (File.Exists(filePath))
            canOverwrite = settings.TryReadFile();
        else
            UserConfigImport.TryImport(settings, userConfigRoot, ownUserConfigFolder);

        // Saving right away finds out whether changes can be saved later, and the first time, it creates the file so another copy's settings are only brought in once.
        settings.CanBeSaved = canOverwrite && settings.Save();

        return settings;
    }

    /// <summary>
    /// Saves one last time if the settings were loaded and can be saved, without throwing, for when the app is about to close after a crash.
    /// </summary>
    public static void TrySaveIfLoaded()
    {
        if (!_default.IsValueCreated || !_default.Value.CanBeSaved)
            return;

        try
        {
            _default.Value.Save();
        }
        catch
        {
            // The settings themselves may be what failed.
        }
    }

    /// <summary>
    /// Sets each setting named in <paramref name="values" /> from its JSON value, or from the text 4.x saved it as.
    /// Unknown names are skipped, and a value that can't be used leaves that setting as it was rather than costing the rest.
    /// </summary>
    public void Populate(IDictionary<string, object> values)
    {
        foreach (var property in _savedProperties)
        {
            if (!values.TryGetValue(property.Name, out var value))
                continue;

            try
            {
                property.SetValue(this, FromJson(value, property.PropertyType));
            }
            catch (FormatException)
            {
                // Like a value mistyped by hand, or one from a newer version that this one doesn't know.
            }
        }
    }

    /// <summary>
    /// Writes the settings to the file now, such as when the widget is closing.
    /// </summary>
    /// <returns>Whether they were saved.</returns>
    public bool Save()
    {
        _saveTimer.Stop();

        var json = Json.Write(_savedProperties.Select(x => new KeyValuePair<string, object>(x.Name, ToJson(x.GetValue(this)))).ToList());

        // The crash handler can save from another thread in the middle of a save on this one.
        lock (_fileLock)
        {
            // The file can be locked for a moment, such as while antivirus or a sync app like OneDrive reads it.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    WriteAllTextAtomically(FilePath, json);
                    return true;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(250);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Loads the settings file named after the running exe, from beside it.
    /// </summary>
    private static Settings LoadBesideExe()
    {
        var filePath = Path.Combine(Path.GetDirectoryName(App.ExePath), Path.GetFileNameWithoutExtension(App.ExePath) + ".settings");

        // Where 4.x kept this copy's settings only matters the first time, so it's only worked out then.
        var ownUserConfigFolder = File.Exists(filePath) ? null : UserConfigImport.TryGetOwnCopyFolder();

        return Load(filePath, UserConfigImport.SettingsRoot, ownUserConfigFolder);
    }

    /// <summary>
    /// Reads the file into these settings, waiting a moment if it's locked, such as by antivirus at sign-in.
    /// </summary>
    /// <returns>False if the file shouldn't be saved over: it couldn't be opened, or it couldn't be read and a copy of it couldn't be kept.</returns>
    private bool TryReadFile()
    {
        string text;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text = reader.ReadToEnd();
                break;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        try
        {
            if (Json.Parse(text) is IDictionary<string, object> values)
            {
                Populate(values);
                return true;
            }
        }
        catch (FormatException)
        {
            // Like a typo made by hand, which is dealt with below.
        }

        // Nothing in it can be read, so start from the defaults, which are then saved over it.
        // A copy of a file that wasn't empty is kept first, so a typo can be put right by hand rather than costing every setting.
        return string.IsNullOrWhiteSpace(text) || TryCopy(FilePath, FilePath + ".bad");
    }

    /// <summary>
    /// Returns a setting's value as JSON: enums by name, and structs like <see cref="WindowPlacement" /> as an object of their fields.
    /// </summary>
    private static object ToJson(object value) => value switch
    {
        null or bool or int or string => value,
        Enum => value.ToString(),
        _ when IsPlainStruct(value.GetType()) => value.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => new KeyValuePair<string, object>(x.Name, ToJson(x.GetValue(value))))
            .ToList(),
        _ => throw new NotSupportedException($"Settings of type {value.GetType()} can't be saved."),
    };

    /// <summary>
    /// Returns a JSON value as <paramref name="type" />.
    /// Numbers and true or false can also be given as text, as 4.x saved them, and enums by name or number.
    /// </summary>
    /// <exception cref="FormatException">The value can't be used as <paramref name="type" />.</exception>
    private static object FromJson(object json, Type type)
    {
        if (type == typeof(string) && json is string)
            return json;

        if (type == typeof(bool))
        {
            return json switch
            {
                bool => json,
                string text when bool.TryParse(text, out var boolean) => boolean,
                _ => throw new FormatException($"{json} isn't true or false."),
            };
        }

        if (type == typeof(int))
        {
            return json switch
            {
                double number when number == Math.Floor(number) && number >= int.MinValue && number <= int.MaxValue => (int)number,
                string text when int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) => number,
                _ => throw new FormatException($"{json} isn't a whole number."),
            };
        }

        if (type.IsEnum)
            return ParseEnum(type, json);

        if (IsPlainStruct(type) && json is IDictionary<string, object> fields)
        {
            // Fields that are missing are left at zero, as they would be in a new struct.
            var value = Activator.CreateInstance(type);

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (fields.TryGetValue(field.Name, out var fieldJson))
                    field.SetValue(value, FromJson(fieldJson, field.FieldType));
            }

            return value;
        }

        throw new FormatException($"{json ?? "null"} isn't a {type.Name}.");
    }

    /// <summary>
    /// Returns the enum value named or numbered by <paramref name="json" />, ignoring case.
    /// A number has to be one of the choices, unless the enum is a set of flags, which can be any combination.
    /// </summary>
    private static object ParseEnum(Type type, object json)
    {
        object value;

        try
        {
            value = json switch
            {
                string text => Enum.Parse(type, text, ignoreCase: true),
                double number when number == Math.Floor(number) => Enum.ToObject(type, (long)number),
                _ => throw new FormatException($"{json ?? "null"} isn't a {type.Name}."),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            throw new FormatException($"{json} isn't a {type.Name}.", ex);
        }

        if (!type.IsDefined(typeof(FlagsAttribute), false) && !Enum.IsDefined(type, value))
            throw new FormatException($"{json} isn't a {type.Name}.");

        return value;
    }

    /// <summary>
    /// Returns whether <paramref name="type" /> is a struct made only of public fields, like <see cref="WindowPlacement" />, which is saved field by field.
    /// Other structs, like <see cref="TimeSpan" />, keep their values in private fields and would be saved empty.
    /// </summary>
    private static bool IsPlainStruct(Type type) =>
        type.IsValueType && !type.IsPrimitive && !type.IsEnum &&
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length > 0 &&
        type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Length == 0;

    /// <summary>
    /// Writes to a temporary file and then swaps it in, so the file is never left empty or half-written if the app is killed mid-save, such as when Windows shuts down.
    /// </summary>
    private static void WriteAllTextAtomically(string path, string contents)
    {
        var tempPath = path + ".tmp";

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(contents);
                writer.Flush();

                // Make sure the new contents are on disk before they replace the old ones.
                stream.Flush(flushToDisk: true);
            }

            // Swap it in with a single rename; File.Replace isn't safe here because it renames the old file away first, so being killed in between leaves no settings file at all.
            if (!MoveFileEx(tempPath, path, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                throw new IOException($"Couldn't replace {path}.", Marshal.GetHRForLastWin32Error());
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static bool TryCopy(string path, string copyPath)
    {
        try
        {
            File.Copy(path, copyPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind, it's replaced by the next save.
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
}
