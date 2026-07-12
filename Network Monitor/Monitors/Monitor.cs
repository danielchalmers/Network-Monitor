using System;
using System.Threading;
using System.Windows.Media;
using Network_Monitor.Properties;

namespace Network_Monitor.Monitors;

public abstract class Monitor : ObservableObject
{
    /// <summary>
    /// Shared timer that publishes every monitor's value in step with the system clock.
    /// Keeping the display refresh on clock seconds means multiple instances of this app (and other clock-synced apps like DesktopClock) update in visual unison.
    /// </summary>
    private static readonly SystemClockTimer PublishTimer = CreatePublishTimer();

    private readonly Timer _timer;
    private readonly object _stateLock = new();
    private string _displayValue;
    private string _latestValue;
    private bool _isPaused;
    private Brush _lightIconBrush;
    private Brush _darkIconBrush;

    protected Monitor(TimeSpan interval)
    {
        if (interval > TimeSpan.Zero)
        {
            _timer = new Timer(_ => Measure());
            _timer.Change(TimeSpan.Zero, interval);

            PublishTimer.SecondChanged += (_, _) => PublishLatest();
        }

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
    /// </summary>
    protected abstract string GetDisplayValue();

    /// <summary>
    /// Measures the latest value and stores it for the next publish.
    /// </summary>
    private void Measure()
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
            _latestValue = value;
    }

    /// <summary>
    /// Publishes the latest measured value to <see cref="DisplayValue" /> unless paused.
    /// </summary>
    private void PublishLatest()
    {
        string value;

        lock (_stateLock)
        {
            if (_isPaused)
                return;

            value = _latestValue;
        }

        if (value is not null)
            DisplayValue = value;
    }

    private static SystemClockTimer CreatePublishTimer()
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
