using System;
using System.ComponentModel;
using System.Windows;

namespace Network_Monitor.Placement
{
    // https://github.com/danielchalmers/WpfWindowPlacement.
    public static class WindowPlacementProperties
    {
        public static readonly DependencyProperty TrackPlacementProperty =
            DependencyProperty.RegisterAttached(
                "TrackPlacement",
                typeof(bool),
                typeof(WindowPlacementProperties),
                new PropertyMetadata(false, OnTrackPlacementChanged));

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.RegisterAttached(
                "Placement",
                typeof(WindowPlacement),
                typeof(WindowPlacementProperties),
                new FrameworkPropertyMetadata(default(WindowPlacement), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPlacementChanged));

        public static bool GetTrackPlacement(Window sender) => (bool)sender.GetValue(TrackPlacementProperty);

        public static void SetTrackPlacement(Window sender, bool value) => sender.SetValue(TrackPlacementProperty, value);

        public static WindowPlacement GetPlacement(Window sender) => (WindowPlacement)sender.GetValue(PlacementProperty);

        public static void SetPlacement(Window sender, WindowPlacement value) => sender.SetValue(PlacementProperty, value);

        private static void OnPlacementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var window = (Window)sender;
            var placement = (WindowPlacement)e.NewValue;

            SetWindowPlacement(window, placement);
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

            SetPlacement(window, placement);
        }

        private static void Window_SourceInitialized(object sender, EventArgs e)
        {
            var window = (Window)sender;
            var placement = GetPlacement(window);

            SetWindowPlacement(window, placement);
        }

        private static void SetWindowPlacement(Window window, WindowPlacement placement)
        {
            // Set window's placement to placement property if it's not default.
            if (!placement.Equals(default(WindowPlacement)))
            {
                WindowPlacementFunctions.SetPlacement(window, placement);
            }
        }
    }
}