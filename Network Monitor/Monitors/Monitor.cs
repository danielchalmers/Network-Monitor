using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

public abstract class Monitor : ObservableObject
{
    private string _displayValue;

    protected Monitor()
    {
    }

    /// <summary>
    /// Name of the monitor.
    /// </summary>
    public string Name { get; protected init; }

    /// <summary>
    /// Icon to show in the UI.
    /// </summary>
    public char Icon { get; protected init; }

    /// <summary>
    /// Icon color.
    /// </summary>
    public Brush IconBrush { get; protected init; }

    /// <summary>
    /// User-friendly text to show in the UI.
    /// </summary>
    public string DisplayValue
    {
        get => _displayValue;
        protected set => Set(ref _displayValue, value);
    }

    /// <summary>
    /// Updates <see cref="DisplayValue"/> with the latest value.
    /// </summary>
    public abstract Task UpdateAsync();
}