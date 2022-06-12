using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Network_Monitor.Monitors;

/// <summary>
/// Dummy monitor for preserving auto-generated window width.
/// </summary>
public class DummyMonitor : Monitor
{
    public DummyMonitor() : base(TimeSpan.Zero)
    {
        Name = "You shouldn't be seeing this!";
        Icon = 'X';
        IconBrush = Brushes.Red;
    }

    protected override Task<string> GetDisplayValueAsync() =>
        throw new InvalidOperationException("Dummy monitor should never be updated.");
}