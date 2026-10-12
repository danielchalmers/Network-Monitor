using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using Network_Monitor.Monitors;

namespace Network_Monitor;

/// <summary>
/// Asks for the address to ping, and only takes a plain IPv4 or IPv6 address of one device, as <see cref="PingAddress" /> reads it.
/// </summary>
public partial class PingAddressDialog : Window
{
    /// <param name="address">The address pinged now, to start from.</param>
    public PingAddressDialog(string address)
    {
        InitializeComponent();

        AddressBox.Text = address;
    }

    /// <summary>
    /// The address typed, without spaces around it, once OK has closed the dialog.
    /// </summary>
    public string Address { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Selected, so a new address can be typed straight over it.
        AddressBox.Focus();
        AddressBox.SelectAll();
    }

    private void ButtonOk_OnClick(object sender, RoutedEventArgs e)
    {
        if (PingAddress.TryParse(AddressBox.Text, out _))
        {
            Address = AddressBox.Text.Trim();
            DialogResult = true;
            return;
        }

        // Say why rather than closing and quietly keeping the old address, and leave what was typed so a typo can be put right.
        MessageText.Text = "Type an IP address like 8.8.8.8 or 2001:4860:4860::8888.";

        // Red, as Windows marks an error, except in a high contrast theme, where only its own colors are sure to be readable.
        if (!SystemParameters.HighContrast)
            MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

        UIElementAutomationPeer.CreatePeerForElement(MessageText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        AddressBox.Focus();
    }
}
