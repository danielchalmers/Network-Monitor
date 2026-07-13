using System.Windows.Media;
using Network_Monitor.Properties;

namespace Network_Monitor.Monitors;

public abstract class Monitor : ObservableObject
{
    /// <summary>
    /// Shared timer that drives every monitor in step with the system clock.
    /// Measuring and publishing on the same clock-second tick makes all monitors change in visual unison at the moment the second changes, alongside other clock-synced apps like DesktopClock.
    /// </summary>
    private static readonly SystemClockTimer ClockTimer = CreateClockTimer();

    private readonly object _stateLock = new();
    private string _displayValue;
    private string _latestValue;
    private bool _isPaused;
    private Brush _lightIconBrush;
    private Brush _darkIconBrush;

    protected Monitor(bool updatesEverySecond)
    {
        if (updatesEverySecond)
            ClockTimer.SecondChanged += (_, _) => Update();

        ThemeService.Instance.PropertyChanged += (_, _) => RaisePropertyChanged(nameof(IconBrush));
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
    /// Icon color for the active theme.
    /// Light and Dark use the fixed Fluent palette (darker shades in light mode, lighter tints in dark mode); Auto follows the system accent color instead.
    /// </summary>
    public Brush IconBrush =>
        Settings.Default.Theme == AppTheme.Auto && ThemeService.Instance.AccentBrush is Brush accent
            ? accent
            : ThemeService.Instance.IsDark ? _darkIconBrush : _lightIconBrush;

    /// <summary>
    /// User-friendly text to show in the UI.
    /// </summary>
    public string DisplayValue
    {
        get => _displayValue;
        protected set => Set(ref _displayValue, value);
    }

    /// <summary>
    /// Whether <see cref="DisplayValue" /> should hold its current value instead of refreshing.
    /// Values are still measured in the background so time-based readings stay accurate, and the latest one is published as soon as the pause ends.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            lock (_stateLock)
                return _isPaused;
        }
        set
        {
            string heldValue;

            lock (_stateLock)
            {
                _isPaused = value;
                heldValue = value ? null : _latestValue;
            }

            if (heldValue is not null)
                DisplayValue = heldValue;
        }
    }

    /// <summary>
    /// Gets the latest value for <see cref="DisplayValue" />.
    /// Called on the shared clock tick, so it must return quickly; implementations that wait on the network should start async work and return the last completed result instead of blocking.
    /// </summary>
    protected abstract string GetDisplayValue();

    /// <summary>
    /// Measures the latest value and publishes it to <see cref="DisplayValue" /> unless paused.
    /// </summary>
    private void Update()
    {
        string value;

        try
        {
            value = GetDisplayValue();
        }
        catch
        {
            value = "Fail";
        }

        lock (_stateLock)
        {
            _latestValue = value;

            if (_isPaused)
                return;
        }

        if (value is not null)
            DisplayValue = value;
    }

    private static SystemClockTimer CreateClockTimer()
    {
        var timer = new SystemClockTimer();
        timer.Start();
        return timer;
    }

    /// <summary>
    /// Sets the icon colors for the light and dark themes.
    /// </summary>
    protected void SetIconColors(string lightHexColor, string darkHexColor)
    {
        _lightIconBrush = CreateFrozenBrush(lightHexColor);
        _darkIconBrush = CreateFrozenBrush(darkHexColor);
        RaisePropertyChanged(nameof(IconBrush));
    }

    private static Brush CreateFrozenBrush(string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }
}
