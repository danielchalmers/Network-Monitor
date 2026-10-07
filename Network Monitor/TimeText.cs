using System;

namespace Network_Monitor;

/// <summary>
/// Formats moments for the hover details.
/// </summary>
public static class TimeText
{
    /// <summary>
    /// Returns a past moment with only as much date as it needs: just the time today, the weekday and time this week, otherwise the date.
    /// </summary>
    public static string FormatMoment(DateTime time, DateTime now)
    {
        if (time.Date == now.Date)
            return time.ToString("t");

        if (time.Date > now.Date.AddDays(-7))
            return time.ToString("ddd ") + time.ToString("t");

        return time.ToString("d");
    }
}
