using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors
{
    public class DummyMonitor : ObservableObject, IMonitor
    {
        private string _displayValue = "1023B";

        public string DisplayValue
        {
            get => _displayValue;
            set => Set(ref _displayValue, value);
        }

        public string Icon { get; } = "X";
        public SolidColorBrush IconColor { get; } = Brushes.Black;
        public string IconToolTip { get; } = "You shouldn't see this";

        public async Task<string> GetNewDisplayValueAsync()
        {
            return DisplayValue;
        }
    }
}