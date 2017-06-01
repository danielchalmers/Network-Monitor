using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Network_Monitor.Properties;

namespace Network_Monitor
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (Settings.Default.MustUpgrade)
            {
                Settings.Default.Upgrade();
                Settings.Default.MustUpgrade = false;
                Settings.Default.Save();
            }
        }

        private void MainWindow_SourceInitialized(object sender, System.EventArgs e)
        {
            if (!string.IsNullOrEmpty(Settings.Default.MainWindowPlacement))
            {
                this.SetPlacement(Settings.Default.MainWindowPlacement);
            }
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Settings.Default.MainWindowPlacement = this.GetPlacement();
            Settings.Default.Save();
        }

        private void MenuItemExit_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}