using System;
using System.Collections.Generic;
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
    private int? _lastUpdateTicks;
    private string _spokenValue;

    /// <summary>
    /// A gap between ticks longer than this means the clock stopped, usually because the PC was asleep.
    /// </summary>
    private const int MaxMillisecondsBetweenTicks = 5000;

    protected Monitor(bool updatesEverySecond)
    {
        if (updatesEverySecond)
            ClockTimer.SecondChanged += (_, _) => Update();

        ThemeService.Instance.PropertyChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(IconBrush));
            RaisePropertyChanged(nameof(TooltipBrush));
        };
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
    /// Color for the graph in the tooltip, which stays light whatever the widget's theme: the accent in Auto, otherwise the light theme's darker shade, which can be seen against it.
    /// </summary>
    public Brush TooltipBrush =>
        Settings.Default.Theme == AppTheme.Auto && ThemeService.Instance.AccentBrush is Brush accent
            ? accent
            : _lightIconBrush;

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
    /// The lowest value the top of the history graph can stand for, which keeps small readings looking small.
    /// </summary>
    public double HistoryMinimumTop { get; protected set; }

    /// <summary>
    /// The reading as a screen reader should say it, such as "Download, 45 megabits per second".
    /// </summary>
    public string SpokenValue
    {
        get => _spokenValue;
        private set => Set(ref _spokenValue, value);
    }

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
    /// Whether the reading just taken shows the network working, like a ping that came back.
    /// Traffic doesn't count, since virtual adapters for Hyper-V, WSL, and Docker keep chattering with no network at all.
    /// </summary>
    protected virtual bool HasLiveReading => false;

    /// <summary>
    /// Gets the latest value for <see cref="SpokenValue" />, from the value just shown.
    /// </summary>
    protected virtual string GetSpokenValue(string displayValue) =>
        displayValue == NoData ? $"{Name}, no reading" : $"{Name}, {displayValue}";

    /// <summary>
    /// Forgets the recent readings behind the hover stats when they no longer describe the last minute, such as after the PC wakes up.
    /// Called on the clock tick, like the other measurement methods.
    /// </summary>
    protected virtual void ResetHistory()
    {
    }

    /// <summary>
    /// Measures the latest value and publishes it to <see cref="DisplayValue" /> and <see cref="Details" /> unless paused.
    /// </summary>
    private void Update()
    {
        var now = Environment.TickCount;

        if (_lastUpdateTicks is int last && unchecked(now - last) > MaxMillisecondsBetweenTicks)
            ResetHistory();

        _lastUpdateTicks = now;

        string value;
        string details;
        IReadOnlyList<double?> history;

        try
        {
            value = GetDisplayValue();
            details = GetDetails();

            // No network at all gets its own explanation, so it isn't mistaken for a failed reading.
            // A ping that came back always wins, so a missed network event can't hide live numbers.
            if (NetworkStatus.IsOffline && !HasLiveReading)
            {
                value = NoData;
                details = $"{Name}{Environment.NewLine}No network connection";
                IsStale = false;
            }

            history = GetHistory();
        }
        catch
        {
            // Reading the counters can fail for a moment, such as when an adapter goes away mid-read, so show the quiet placeholder and try again next tick.
            value = NoData;
            details = $"{Name}{Environment.NewLine}Couldn't take a reading";
            history = GetHistorySafely();
        }

        lock (_stateLock)
        {
            _latestValue = value;
            _latestDetails = details;

            if (_isPaused)
                return;
        }

        if (value is not null)
        {
            DisplayValue = value;
            SpokenValue = GetSpokenValue(value);
        }

        Details = details;
        History = history;
    }

    /// <summary>
    /// Returns the current history, or none if even that fails, so a failed reading never brings back a graph that was just cleared.
    /// </summary>
    private IReadOnlyList<double?> GetHistorySafely()
    {
        try
        {
            return GetHistory();
        }
        catch
        {
            return Array.Empty<double?>();
        }
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
        RaisePropertyChanged(nameof(TooltipBrush));
    }

    private static Brush CreateFrozenBrush(string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }
}
