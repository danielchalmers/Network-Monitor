using System;

namespace Network_Monitor.Monitors;

/// <summary>
/// Dummy monitor for preserving auto-generated window width.
/// </summary>
public class DummyMonitor : Monitor
{
    public DummyMonitor() : base(false)
    {
        Name = "You shouldn't be seeing this!";
        Icon = 'X';
        SetIconColors("#D13438", "#D13438"); // Fluent red primary; never visible.
        DisplayValue = string.Empty.PadRight(4);
    }

    protected override string GetDisplayValue() =>
        throw new InvalidOperationException("Dummy monitor should never be updated.");
}