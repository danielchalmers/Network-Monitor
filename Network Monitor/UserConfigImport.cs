using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Network_Monitor.Properties;
using WpfWindowPlacement;

namespace Network_Monitor;

/// <summary>
/// Brings in the settings 4.x saved, the first time a copy of 5.0 runs.
/// 4.x left them to .NET, which keeps a user.config for each exe path and version.
/// A copy that 4.x ran from the same path, like the installed one, gets its own back, as it did when 4.x updated.
/// The portable exe has a new name in 5.0, and the installer and the portable exe never shared settings, so any other copy gets the most recently saved file, whichever copy saved it.
/// </summary>
/// <remarks>
/// The old files are only read, so going back to 4.x finds its settings as they were.
/// </remarks>
public static class UserConfigImport
{
    private const string FileName = "user.config";
    private const string SectionName = "Network_Monitor.Properties.Settings";

    /// <summary>
    /// Where 4.x kept its settings: a folder named after the company, with the space made an underscore, holding a folder for each exe path.
    /// </summary>
    public static string SettingsRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Network_Monitor");

    /// <summary>
    /// Returns the folder under <see cref="SettingsRoot" /> where 4.x kept the settings of the copy at this exe's path, with a folder in it for each version, or <c>null</c> if it can't be worked out.
    /// </summary>
    public static string TryGetOwnCopyFolder()
    {
        try
        {
            // The folder is named after a hash of the exe's path that only .NET knows how to work out.
            var userConfigPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            return Path.GetDirectoryName(Path.GetDirectoryName(userConfigPath));
        }
        catch (ConfigurationErrorsException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads 4.x's settings into <paramref name="settings" />, which keep their defaults if there are none or they can't be read.
    /// It takes this copy's own 4.x settings in <paramref name="ownCopyFolder" /> if it can read them, or else the most recently saved 4.x settings under <paramref name="settingsRoot" />.
    /// </summary>
    /// <param name="settingsRoot">Where 4.x kept its settings, like <see cref="SettingsRoot" />.</param>
    /// <param name="ownCopyFolder">The folder under it where 4.x kept this copy's settings, from <see cref="TryGetOwnCopyFolder" />, or <c>null</c> if that isn't known.</param>
    /// <returns>The file that was read, or <c>null</c> if none was.</returns>
    public static string TryImport(Settings settings, string settingsRoot, string ownCopyFolder)
    {
        try
        {
            var source = (ownCopyFolder != null ? FindNewestReadable(ownCopyFolder) : null) ?? FindNewestReadable(settingsRoot);

            if (source != null)
                ReadInto(settings, source);

            return source;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            // Starting from defaults is better than not starting.
            return null;
        }
    }

    /// <summary>
    /// Returns the most recently written settings file under <paramref name="settingsRoot" /> that can be read.
    /// </summary>
    public static string FindNewestReadable(string settingsRoot)
    {
        if (!Directory.Exists(settingsRoot))
            return null;

        return new DirectoryInfo(settingsRoot)
            .EnumerateFiles(FileName, SearchOption.AllDirectories)
            .Where(x => x.Length > 0 && IsReadable(x.FullName))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    /// <summary>
    /// Reads the settings in a 4.x user.config into <paramref name="settings" />.
    /// Ones that are missing or can't be read keep the values they had, and ones 5.0 doesn't have are skipped.
    /// </summary>
    public static void ReadInto(Settings settings, string path)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in GetSettings(XDocument.Load(path)))
        {
            var name = (string)setting.Attribute("name");
            var value = setting.Element("value");

            if (name is null || value is null)
                continue;

            if (name == nameof(Settings.Placement))
            {
                if (TryReadPlacement(value) is WindowPlacement placement)
                    settings.Placement = placement;
            }
            else
            {
                // The rest were saved as text, like "True" or "96", which the settings read the same way as JSON.
                values[name] = value.Value;
            }
        }

        settings.Populate(values);
    }

    private static bool IsReadable(string path)
    {
        try
        {
            return GetSettings(XDocument.Load(path)).Any();
        }
        catch
        {
            // A file cut short by a power loss isn't worth bringing along.
            return false;
        }
    }

    private static IEnumerable<XElement> GetSettings(XDocument document) =>
        document.Root?.Element("userSettings")?.Element(SectionName)?.Elements("setting") ?? Enumerable.Empty<XElement>();

    /// <summary>
    /// Reads the window position, which .NET saved with <see cref="XmlSerializer" />.
    /// </summary>
    private static WindowPlacement? TryReadPlacement(XElement value)
    {
        var element = value.Elements().FirstOrDefault();

        if (element is null)
            return null;

        try
        {
            using var reader = element.CreateReader();
            return (WindowPlacement)new XmlSerializer(typeof(WindowPlacement)).Deserialize(reader);
        }
        catch (InvalidOperationException)
        {
            // Without it, the widget opens where Windows puts new windows, and it's simply moved.
            return null;
        }
    }
}
