using System;
using System.Threading;

namespace Network_Monitor;

/// <summary>
/// A timer that is synced with the system clock.
/// </summary>
/// <remarks>
/// <see href="https://github.com/danielchalmers/DesktopClock" />
/// </remarks>
public sealed class SystemClockTimer : IDisposable
{
    /// <summary>
    /// A second boundary closer than this is skipped in favor of the next one.
    /// </summary>
    private const int MinimumMillisecondsUntilTick = 200;

    private readonly Timer _timer;
    private int _ticking;

    public SystemClockTimer()
    {
        _timer = new Timer(_ => OnTick());
    }

    /// <summary>
    /// Occurs after the second of the system clock changes.
    /// </summary>
    public event EventHandler SecondChanged;

    public void Dispose() => _timer.Dispose();

    public void Start() => ScheduleTickForNextSecond();

    public void Stop() => _timer.Change(Timeout.Infinite, Timeout.Infinite);

    private void OnTick()
    {
        ScheduleTickForNextSecond();

        // A tick that's still running when the next one starts is left to finish instead of running two at once, which would trip over each other's readings.
        if (Interlocked.Exchange(ref _ticking, 1) != 0)
            return;

        try
        {
            SecondChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            Interlocked.Exchange(ref _ticking, 0);
        }
    }

    /// <summary>
    /// Starts the timer and schedules the tick for the next second on the system clock.
    /// </summary>
    private void ScheduleTickForNextSecond() =>
        _timer.Change(GetMillisecondsUntilTick(DateTimeOffset.Now.Millisecond), Timeout.Infinite);

    /// <summary>
    /// Returns how long to wait for the next tick, which lands on the next second boundary unless that's only moments away.
    /// A late tick, like the first one after the PC wakes up or the clock is adjusted, would otherwise be followed by another a few milliseconds later, and readings taken over that sliver of a second come out wildly wrong.
    /// </summary>
    public static int GetMillisecondsUntilTick(int currentMillisecond)
    {
        var milliseconds = 1000 - currentMillisecond;

        return milliseconds < MinimumMillisecondsUntilTick ? milliseconds + 1000 : milliseconds;
    }
}