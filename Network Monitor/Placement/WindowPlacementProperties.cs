using System;
using System.ComponentModel;
using System.Windows;

namespace Network_Monitor.Placement;

/// <summary>
/// <para><see cref="WindowPlacement"/> related attached properties for use in XAML.</para>
/// <para>See <see cref="WindowPlacementFunctions"/> for use in code-behind.</para>
/// </summary>
// https://github.com/danielchalmers/WpfWindowPlacement.
public static class WindowPlacementProperties
{
    /// <summary>
    /// Set window's placement to <see cref="PlacementProperty"/> on source initialization, and vice versa on window closing.
    /// </summary>
    public static readonly DependencyProperty TrackPlacementProperty =
        DependencyProperty.RegisterAttached(
            "TrackPlacement",
            typeof(bool),
            typeof(WindowPlacementProperties),
            new PropertyMetadata(false, OnTrackPlacementChanged));

    /// <summary>
    /// <see cref="WindowPlacement"/> property to use with <see cref="TrackPlacementProperty"/>.
    /// </summary>
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.RegisterAttached(
            "Placement",
            typeof(WindowPlacement),
            typeof(WindowPlacementProperties),
            new FrameworkPropertyMetadata(default(WindowPlacement), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPlacementChanged));

    /// <summary>
    /// Get <see cref="TrackPlacementProperty"/> attached property.
    /// </summary>
    /// <param name="sender">Window to track.</param>
    public static bool GetTrackPlacement(Window sender) => (bool)sender.GetValue(TrackPlacementProperty);

    /// <summary>
    /// Set <see cref="TrackPlacementProperty"/> attached property.
    /// </summary>
    /// <param name="sender">Window to track.</param>
    /// <param name="value">Enable tracking.</param>
    public static void SetTrackPlacement(Window sender, bool value) => sender.SetValue(TrackPlacementProperty, value);

    /// <summary>
    /// Get <see cref="PlacementProperty"/> attached property.
    /// </summary>
    public static WindowPlacement GetPlacement(Window sender) => (WindowPlacement)sender.GetValue(PlacementProperty);

    /// <summary>
    /// Set <see cref="PlacementProperty"/> attached property.
    /// </summary>
    public static void SetPlacement(Window sender, WindowPlacement value) => sender.SetValue(PlacementProperty, value);

    private static void OnPlacementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)sender;
        var placement = (WindowPlacement)e.NewValue;

        WindowPlacementFunctions.SetPlacement(window, placement);
    }

    private static void OnTrackPlacementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)sender;
        var track = (bool)e.NewValue;

        if (track)
        {
            window.SourceInitialized += Window_SourceInitialized;
            window.Closing += Window_Closing;
        }
        else
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.Closing -= Window_Closing;
        }
    }

    private static void Window_Closing(object sender, CancelEventArgs e)
    {
        var window = (Window)sender;
        var placement = WindowPlacementFunctions.GetPlacement(window);

        // Set PlacementProperty to sender window placement.
        SetPlacement(window, placement);
    }

    private static void Window_SourceInitialized(object sender, EventArgs e)
    {
        var window = (Window)sender;
        var placement = GetPlacement(window);

        // Set sender window's placement to PlacementProperty.
        WindowPlacementFunctions.SetPlacement(window, placement);
    }
}