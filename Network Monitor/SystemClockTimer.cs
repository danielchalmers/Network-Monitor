﻿using System;
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
    private readonly Timer _timer;

    public SystemClockTimer()
    {
        _timer = new Timer(_ => OnTick());
    }

    /// <summary>
    /// Occurs after the second of the system clock changes.
    /// </summary>
    public event EventHandler SecondChanged;

    /// <summary>
    /// Number of milliseconds until the next second on the system clock.
    /// </summary>
    private int MillisecondsUntilNextSecond => 1000 - DateTimeOffset.Now.Millisecond;

    public void Dispose() => _timer.Dispose();

    public void Start() => ScheduleTickForNextSecond();

    public void Stop() => _timer.Change(Timeout.Infinite, Timeout.Infinite);

    private void OnTick()
    {
        ScheduleTickForNextSecond();

        SecondChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Starts the timer and schedules the tick for the next second on the system clock.
    /// </summary>
    private void ScheduleTickForNextSecond() =>
        _timer.Change(MillisecondsUntilNextSecond, Timeout.Infinite);
}