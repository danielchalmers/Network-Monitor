using System.Windows;
using System.Windows.Controls;
using Network_Monitor.Monitors;

namespace Network_Monitor;

/// <summary>
/// Interaction logic for MonitorView.xaml
/// </summary>
public partial class MonitorView : UserControl
{
    public static readonly DependencyProperty MonitorProperty =
        DependencyProperty.Register(nameof(Monitor), typeof(Monitor), typeof(MonitorView));

    public MonitorView()
    {
        InitializeComponent();
    }

    public Monitor Monitor
    {
        get => (Monitor)GetValue(MonitorProperty);
        set => SetValue(MonitorProperty, value);
    }
}
