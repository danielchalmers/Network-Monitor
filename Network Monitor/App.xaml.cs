using System.Diagnostics;
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

    /// <summary>
    /// The full path of the running exe.
    /// </summary>
    public static string ExePath { get; } = Process.GetCurrentProcess().MainModule.FileName;

    protected override void OnStartup(StartupEventArgs e)
    {
        CrashHandler.Register(this);
        base.OnStartup(e);

        // The window is created here rather than by StartupUri so the settings are ready before anything reads them.
        if (!SettingsStartup.Prepare(e.Args))
        {
            Shutdown();
            return;
        }

        SettingsSaver.Start();
        RepairStartup();

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    private static void RepairStartup()
    {
#if !DEBUG // A debug build shouldn't take over the user's own startup entry.
        try
        {
            StartupRegistration.RepairOnLaunch(ExePath);
        }
        catch
        {
            // A policy can make the Run key read-only.
        }
#endif
    }
}