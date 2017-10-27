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

            ViewModel = new MainViewModel();

            DataContext = ViewModel;
        }

        public MainViewModel ViewModel { get; }

        private void Window_OnClosed(object sender, System.EventArgs e)
        {
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

        private async void Window_SourceInitialized(object sender, System.EventArgs e)
        {
            await ViewModel.StartMonitoring();
        }
    }
}