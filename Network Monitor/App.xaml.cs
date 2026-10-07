using System.Reflection;
using System.Windows;

namespace Network_Monitor;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The app's version, such as "4.1.0", for anywhere it's reported, like crash logs.
    /// </summary>
    public static string VersionText { get; } =
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        CrashHandler.Register(this);
        base.OnStartup(e);
    }
}