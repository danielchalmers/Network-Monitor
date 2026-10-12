using System.Reflection;

namespace Network_Monitor.Tests;

/// <summary>
/// The installer and the release workflow pick up "Network Monitor.exe", and an installed copy's settings file is named after it.
/// A copy that's running is recognized by its product name, so a new copy bringing in 4.x's settings doesn't open on top of it.
/// </summary>
public class AssemblyIdentityTests
{
    private static readonly Assembly AppAssembly = typeof(App).Assembly;

    [Fact]
    public void AssemblyName_ShouldStayTheSame()
    {
        Assert.Equal("Network Monitor", AppAssembly.GetName().Name);
    }

    [Fact]
    public void Product_ShouldStayTheSame()
    {
        Assert.Equal("Network Monitor", AppAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }

    // The version is shown to users, so it should read "4.1.0" rather than carry a commit hash.
    [Fact]
    public void InformationalVersion_ShouldNotIncludeTheCommitHash()
    {
        var version = AppAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.DoesNotContain("+", version);
    }
}
