using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors
{
    public interface IMonitor
    {
        string DisplayValue { get; set; }
        string Icon { get; }
        SolidColorBrush IconColor { get; }

        Task<string> GetNewDisplayValueAsync();
    }
}