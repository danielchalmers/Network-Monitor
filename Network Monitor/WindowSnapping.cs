using System;
using System.Windows;

namespace Network_Monitor;

public static class WindowSnapping
{
    public static double GetMargin(DependencyObject obj) =>
        (double)obj.GetValue(MarginProperty);

    public static void SetMargin(DependencyObject obj, double value) => 
        obj.SetValue(MarginProperty, value);

    public static readonly DependencyProperty MarginProperty =
        DependencyProperty.RegisterAttached("Margin",
            typeof(double?), typeof(WindowSnapping),
            new PropertyMetadata(null, OnMarginChanged));

    private static void OnMarginChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)sender;

        window.LocationChanged -= Window_LocationChanged;
        window.LocationChanged += Window_LocationChanged;
    }

    private static void Window_LocationChanged(object sender, EventArgs e)
    {
        var window = (Window)sender;
        var margin = GetMargin(window);

        if (Math.Abs(window.Top) < margin)
            window.Top = 0;
        else if (Math.Abs(SystemParameters.PrimaryScreenHeight - window.ActualHeight - window.Top) < margin)
            window.Top = SystemParameters.PrimaryScreenHeight - window.ActualHeight;

        if (Math.Abs(window.Left) < margin)
            window.Left = 0;
        else if (Math.Abs(SystemParameters.PrimaryScreenWidth - window.ActualWidth - window.Left) < margin)
            window.Left = SystemParameters.PrimaryScreenWidth - window.ActualWidth;
    }
}