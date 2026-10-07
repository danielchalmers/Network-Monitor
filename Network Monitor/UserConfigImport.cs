using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Network_Monitor;

/// <summary>
/// Brings in another copy's settings the first time a copy of the app runs.
/// Windows keeps settings per exe path, so a portable download saved under a new name or in a new folder, or switching between the installer and the portable exe, would otherwise start from scratch.
/// </summary>
public static class UserConfigImport
{
    private const string FileName = "user.config";
    private const string SectionName = "Network_Monitor.Properties.Settings";

    /// <summary>
    /// Copies the most recently saved settings of another copy into <paramref name="userConfigPath" /> when this copy has never saved settings in any version.
    /// The other copies' files are only read.
    /// </summary>
    /// <param name="userConfigPath">Where this copy keeps its settings for the current version.</param>
    /// <param name="includePlacement">Whether to bring the window position along; leaving it out keeps a new copy from opening exactly on top of one that's still running.</param>
    /// <returns>The file that was imported, or <c>null</c> if nothing was.</returns>
    public static string ImportIfNewCopy(string userConfigPath, bool includePlacement)
    {
        var versionFolder = Path.GetDirectoryName(userConfigPath);
        var copyFolder = Path.GetDirectoryName(versionFolder);
        var settingsRoot = Path.GetDirectoryName(copyFolder);

        // A settings file for any version means this copy has run before, and in-place updates keep going through Upgrade().
        if (Directory.Exists(copyFolder) && Directory.EnumerateFiles(copyFolder, FileName, SearchOption.AllDirectories).Any())
            return null;

        var source = FindNewestReadable(settingsRoot, copyFolder);

        if (source is null)
            return null;

        var document = XDocument.Load(source);
        EnsureSectionDeclared(document);

        // Starting with Windows belongs to whichever exe it was turned on for, so the new copy starts with it off rather than claiming a startup entry that runs another exe.
        GetSettings(document).Where(x => (string)x.Attribute("name") == "RunOnStartup").Remove();

        if (!includePlacement)
            GetSettings(document).Where(x => (string)x.Attribute("name") == "Placement").Remove();

        // Written next to the destination and then moved into place, so a crash halfway through can't leave a half-written settings file.
        Directory.CreateDirectory(versionFolder);
        var tempPath = userConfigPath + ".import";
        document.Save(tempPath);
        File.Move(tempPath, userConfigPath);

        return source;
    }

    /// <summary>
    /// Returns the most recently written settings file under <paramref name="settingsRoot" /> that can be read, skipping anything in <paramref name="excludedFolder" />.
    /// </summary>
    public static string FindNewestReadable(string settingsRoot, string excludedFolder)
    {
        if (!Directory.Exists(settingsRoot))
            return null;

        var excludedPrefix = excludedFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return new DirectoryInfo(settingsRoot)
            .EnumerateFiles(FileName, SearchOption.AllDirectories)
            .Where(x => !x.FullName.StartsWith(excludedPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Length > 0 && IsReadable(x.FullName))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
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
    /// Adds the declaration of the settings section if the file doesn't have one.
    /// Files written by a build that came with its .exe.config, such as a debug build, leave it out, and the published exe can't load them without it.
    /// </summary>
    private static void EnsureSectionDeclared(XDocument document)
    {
        var configSections = document.Root.Element("configSections");

        if (configSections is null)
        {
            configSections = new XElement("configSections");
            document.Root.AddFirst(configSections);
        }

        var group = configSections.Elements("sectionGroup").FirstOrDefault(x => (string)x.Attribute("name") == "userSettings");

        if (group is null)
        {
            group = new XElement("sectionGroup",
                new XAttribute("name", "userSettings"),
                new XAttribute("type", "System.Configuration.UserSettingsGroup, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));
            configSections.Add(group);
        }

        if (group.Elements("section").Any(x => (string)x.Attribute("name") == SectionName))
            return;

        group.Add(new XElement("section",
            new XAttribute("name", SectionName),
            new XAttribute("type", "System.Configuration.ClientSettingsSection, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            new XAttribute("allowExeDefinition", "MachineToLocalUser"),
            new XAttribute("requirePermission", "false")));
    }
}
