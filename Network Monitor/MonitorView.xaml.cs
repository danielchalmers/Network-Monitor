using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Network_Monitor.Monitors;
using Network_Monitor.Properties;

namespace Network_Monitor;

/// <summary>
/// Interaction logic for MonitorView.xaml
/// </summary>
public partial class MonitorView : UserControl
{
    public static readonly DependencyProperty MonitorProperty =
        DependencyProperty.Register(nameof(Monitor), typeof(Monitor), typeof(MonitorView));

    /// <summary>
    /// Space between the widget and its tooltip, in device pixels.
    /// </summary>
    private const double ToolTipGap = 4;

    private static ToolTip _openToolTip;

    public MonitorView()
    {
        InitializeComponent();
    }

    public Monitor Monitor
    {
        get => (Monitor)GetValue(MonitorProperty);
        set => SetValue(MonitorProperty, value);
    }

    /// <summary>
    /// When tooltips can show again. They're held back while the widget is dragged and just after.
    /// </summary>
    public static DateTime ToolTipsResumeAt { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Puts a reading's tooltip beside the widget, across from the way the readings flow: right of a column and below a strip, or on the other side when there's no room on that side of the screen.
    /// </summary>
    /// <remarks>
    /// Sizes and points are in device pixels relative to the reading's top-left corner.
    /// WPF takes the first placement that fits on screen, otherwise whichever shows the most, shifted onto the screen.
    /// </remarks>
    public static readonly CustomPopupPlacementCallback PlaceBesideWidget = (popupSize, targetSize, _) =>
        Settings.Default.Horizontal
            ? new[]
            {
                new CustomPopupPlacement(new Point(0, targetSize.Height + ToolTipGap), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(0, -popupSize.Height - ToolTipGap), PopupPrimaryAxis.Horizontal),
            }
            : new[]
            {
                new CustomPopupPlacement(new Point(targetSize.Width + ToolTipGap, 0), PopupPrimaryAxis.Vertical),
                new CustomPopupPlacement(new Point(-popupSize.Width - ToolTipGap, 0), PopupPrimaryAxis.Vertical),
            };

    private void Row_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (DateTime.UtcNow < ToolTipsResumeAt)
            e.Handled = true;
    }

    /// <summary>
    /// Closes whichever reading's tooltip is open.
    /// One that's open when a drag starts would otherwise stay behind where the widget was.
    /// </summary>
    public static void CloseToolTip()
    {
        if (_openToolTip != null)
            _openToolTip.IsOpen = false;
    }

    private void ToolTip_Opened(object sender, RoutedEventArgs e)
    {
        _openToolTip = (ToolTip)sender;
    }
}
