using System.Reflection;
using System.Windows;

namespace Network_Monitor;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static string Title { get; } = Assembly.GetExecutingAssembly().GetName().Name;
}