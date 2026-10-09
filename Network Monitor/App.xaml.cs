using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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
        // The uninstall waits for this, so it comes before anything that could show a message or hand off to a copy that's still running.
        if (e.Args.Contains(UninstallCleanup.Argument))
        {
            UninstallCleanup.Run(ExePath);
            Shutdown();
            return;
        }

        CrashHandler.Register(this);

        // Three numbers gain nothing from the graphics card, and drawing them in software saves about 10 MB of memory with no visible difference.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        base.OnStartup(e);

        if (!SingleInstance.TryClaim(ExePath))
        {
            Shutdown();
            return;
        }

        // The window is created here rather than by StartupUri so the settings are ready before anything reads them.
        if (!SettingsStartup.Prepare(e.Args))
        {
            Shutdown();
            return;
        }

        SettingsSaver.Start();
        RepairStartup();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        SingleInstance.ListenForShowRequests(() => Dispatcher.BeginInvoke(new Action(window.Reveal)));
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