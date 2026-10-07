using System.Reflection;

namespace Network_Monitor.Tests;

/// <summary>
/// .NET keeps each user's settings in %LOCALAPPDATA%\Network_Monitor\&lt;exe&gt;_Url_&lt;hash&gt;\&lt;version&gt;, a folder named after the company and the exe.
/// Renaming the assembly or the company would silently reset everyone's settings; a new version is fine because Settings.Upgrade carries them over.
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
    public void Company_ShouldStayTheSame()
    {
        Assert.Equal("Network Monitor", AppAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company);
    }

    // The version is shown to users, so it should read "4.1.0" rather than carry a commit hash.
    [Fact]
    public void InformationalVersion_ShouldNotIncludeTheCommitHash()
    {
        var version = AppAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.DoesNotContain("+", version);
    }
}
