using System;
using System.Windows;
using System.Windows.Controls;
using Network_Monitor.Monitors;

namespace Network_Monitor;

/// <summary>
/// Interaction logic for MonitorView.xaml
/// </summary>
public partial class MonitorView : UserControl
{
    private readonly SystemClockTimer Timer = new();

    public static readonly DependencyProperty MonitorProperty = DependencyProperty.Register(nameof(Monitor), typeof(Monitor), typeof(MonitorView), new(OnMonitorChanged));

    public MonitorView()
    {
        InitializeComponent();
    }

    public Monitor Monitor
    {
        get => (Monitor)GetValue(MonitorProperty);
        set => SetValue(MonitorProperty, value);
    }

    public static void OnMonitorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is not null)
            throw new InvalidOperationException("The monitor can only be set once.");

        var view = (MonitorView)d;
        var monitor = (Monitor)e.NewValue;

        view.DisplayTextBlock.Text = monitor.DisplayValue;

        view.Timer.SecondChanged += (_, _) => view.Dispatcher.Invoke(() => view.DisplayTextBlock.Text = monitor.DisplayValue);
        view.Timer.Start();
    }
}
