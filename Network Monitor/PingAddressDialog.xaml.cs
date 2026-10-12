using System;
using System.Media;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
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

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        ThemeService.Instance.ApplyToWindow(this);
    }

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

        // Say why, with a beep like the widget's other failures, rather than closing and quietly keeping the old address.
        // What was typed stays, so a typo can be put right, and the message names what often comes along with a pasted address, like the %12 that ipconfig shows after a router's IPv6 address.
        MessageText.Text = "That isn't a plain IP address of one device. Type one like 8.8.8.8 or 2001:4860:4860::8888, without a name like google.com, a zone like %12, a port, or leading zeros.";

        // Red, as Windows marks an error, in a shade that can be read in the dark theme too, and the text color in a high contrast theme, where only its own colors are sure to be readable.
        MessageText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorTextBrush");

        UIElementAutomationPeer.CreatePeerForElement(MessageText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        SystemSounds.Beep.Play();
        AddressBox.Focus();
    }
}
