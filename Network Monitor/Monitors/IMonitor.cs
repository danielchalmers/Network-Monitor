using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors
{
    public interface IMonitor
    {
        string DisplayValue { get; }
        string Icon { get; }
        SolidColorBrush IconColor { get; }
        string IconToolTip { get; }

        Task<string> GetNewDisplayValueAsync();

        Task UpdateDisplayValueAsync();
    }
}