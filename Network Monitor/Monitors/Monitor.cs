﻿using System;
using System.Threading;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

public abstract class Monitor
{
    private readonly Timer _timer;

    protected Monitor(TimeSpan interval)
    {
        if (interval > TimeSpan.Zero)
        {
            _timer = new Timer(_ => Update());
            _timer.Change(TimeSpan.Zero, interval);
        }
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
    public string DisplayValue { get; protected set; }

    /// <summary>
    /// Gets the latest value for <see cref="DisplayValue" />.
    /// </summary>
    protected abstract string GetDisplayValue();

    /// <summary>
    /// Updates <see cref="DisplayValue"/> with the latest value.
    /// </summary>
    private void Update()
    {
        try
        {
            DisplayValue = GetDisplayValue();
        }
        catch
        {
            DisplayValue = "Fail";
        }
    }
}