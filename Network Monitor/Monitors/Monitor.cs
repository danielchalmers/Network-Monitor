using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

public abstract class Monitor : ObservableObject
{
    private string _displayValue = string.Empty.PadRight(4);

    protected Monitor()
    {
    }

    /// <summary>
    /// Name of the monitor.
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// Icon to show in the UI.
    /// </summary>
    public char Icon { get; protected set; }

    /// <summary>
    /// Icon color.
    /// </summary>
    public Brush IconBrush { get; protected set; }

    /// <summary>
    /// User-friendly text to show in the UI.
    /// </summary>
    public string DisplayValue
    {
        get => _displayValue;
        private set
        {
            if (value == null || value.Length > 4)
                throw new InvalidOperationException("Value can't be null or more than 4 characters.");

            Set(ref _displayValue, value);
        }
    }

    /// <summary>
    /// Gets the latest value for <see cref="DisplayValue" />.
    /// </summary>
    protected abstract Task<string> GetDisplayValueAsync();

    /// <summary>
    /// Updates <see cref="DisplayValue"/> with the latest value.
    /// </summary>
    public async Task UpdateAsync()
    {
        try
        {
            DisplayValue = await GetDisplayValueAsync();
        }
        catch
        {
            DisplayValue = "Fail";
        }
    }
}