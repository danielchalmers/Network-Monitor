using System.Threading.Tasks;
using Network_Monitor.Monitors.Models;

namespace Network_Monitor.Monitors
{
    public interface IMonitor
    {
        /// <summary>
        /// User-friendly text to show in UI.
        /// </summary>
        string DisplayValue { get; }

        MonitorIcon Icon { get; }

        /// <summary>
        /// Update <see cref="DisplayValue"/> with latest value.
        /// </summary>
        Task UpdateAsync();
    }
}