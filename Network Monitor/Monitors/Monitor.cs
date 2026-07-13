using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
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

    /// <summary>
    /// Placeholder shown when no reading is available, such as before the first measurement or while the network is down.
    /// </summary>
    protected const string NoData = "—";

    private readonly object _stateLock = new();
    private string _displayValue;
    private string _latestValue;
    private string _details;
    private string _latestDetails;
    private IReadOnlyList<double?> _history = Array.Empty<double?>();
    private bool _isPaused;
    private bool _isStale;
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
    /// Multi-line diagnostic text shown when hovering over the monitor.
    /// </summary>
    public string Details
    {
        get => _details;
        private set => Set(ref _details, value);
    }

    /// <summary>
    /// Recent readings for the hover sparkline, oldest first. Null entries are gaps (e.g. a lost ping).
    /// </summary>
    public IReadOnlyList<double?> History
    {
        get => _history;
        private set => Set(ref _history, value);
    }

    /// <summary>
    /// Whether the sparkline's vertical scale is anchored at zero to show magnitude (throughput) rather than spanning min–max to show variation (latency jitter).
    /// </summary>
    public bool HistoryStartsAtZero { get; protected set; }

    /// <summary>
    /// Whether <see cref="DisplayValue" /> is older than expected because a fresh reading hasn't arrived on schedule.
    /// The UI dims stale values so they aren't mistaken for live ones.
    /// </summary>
    public bool IsStale
    {
        get => _isStale;
        protected set => Set(ref _isStale, value);
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
            string heldDetails;

            lock (_stateLock)
            {
                _isPaused = value;
                heldValue = value ? null : _latestValue;
                heldDetails = value ? null : _latestDetails;
            }

            if (heldValue is not null)
                DisplayValue = heldValue;

            if (heldDetails is not null)
                Details = heldDetails;
        }
    }

    /// <summary>
    /// Gets the latest value for <see cref="DisplayValue" />.
    /// Called on the shared clock tick, so it must return quickly; implementations that wait on the network should start async work and return the last completed result instead of blocking.
    /// </summary>
    protected abstract string GetDisplayValue();

    /// <summary>
    /// Gets the latest value for <see cref="Details" />.
    /// Called right after <see cref="GetDisplayValue" /> on the same tick, so the two always describe the same reading.
    /// </summary>
    protected virtual string GetDetails() => Name;

    /// <summary>
    /// Gets the recent readings for <see cref="History" />, oldest first, using null for gaps.
    /// </summary>
    protected virtual IReadOnlyList<double?> GetHistory() => Array.Empty<double?>();

    /// <summary>
    /// Measures the latest value and publishes it to <see cref="DisplayValue" /> and <see cref="Details" /> unless paused.
    /// </summary>
    private void Update()
    {
        string value;
        string details;

        try
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                value = GetDisplayValue();
                details = GetDetails();
            }
            else
            {
                // No network at all is a distinct state from a failed reading, so show a quiet placeholder instead of an alarming "Fail".
                value = NoData;
                details = $"{Name}{Environment.NewLine}No network connection";
                IsStale = false;
            }
        }
        catch
        {
            value = "Fail";
            details = Name;
        }

        lock (_stateLock)
        {
            _latestValue = value;
            _latestDetails = details;

            if (_isPaused)
                return;
        }

        if (value is not null)
            DisplayValue = value;

        Details = details;
        History = GetHistory();
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
