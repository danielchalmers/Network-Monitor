using System;
using System.Collections.Generic;

namespace Network_Monitor.Monitors;

/// <summary>
/// Statistics over round trip times.
/// </summary>
public static class LatencyMath
{
    /// <summary>
    /// Returns the average difference between consecutive round trip times, which is what makes a connection feel unstable even when the average latency looks fine.
    /// </summary>
    public static double GetJitter(IReadOnlyList<long> roundtripTimes)
    {
        if (roundtripTimes.Count < 2)
            return 0;

        double totalDifference = 0;

        for (var i = 1; i < roundtripTimes.Count; i++)
            totalDifference += Math.Abs(roundtripTimes[i] - roundtripTimes[i - 1]);

        return totalDifference / (roundtripTimes.Count - 1);
    }
}
