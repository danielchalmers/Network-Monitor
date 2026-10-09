using System;

namespace Network_Monitor.Monitors;

/// <summary>
/// Decides what the latency reading says when pings stop coming back, and how long outages read.
/// </summary>
public static class Outage
{
    /// <summary>
    /// How long without a reply before the reading turns into a clock.
    /// A ping or two can go missing on a healthy connection, so until then the last number just dims.
    /// </summary>
    public const int ClockAfterSeconds = 3;

    /// <summary>
    /// How recently a reply must have arrived to show as a live reading.
    /// On a connection whose replies take longer than a second, there's always a newer ping still out, but replies still arrive every second.
    /// </summary>
    public static readonly TimeSpan LiveFor = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// How long the tooltip mentions an outage once replies are back.
    /// </summary>
    public static readonly TimeSpan RecoveryShownFor = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Returns how long an outage has lasted in at most four characters, for the widget: "0:03" to "9:59", then "0h10" to "9h59", then "10h" and up.
    /// There's no "m" for minutes, because on the rows below "m" means megabits; the colon is what says it's a time.
    /// </summary>
    public static string FormatClock(TimeSpan duration)
    {
        var seconds = Math.Max(0, (long)duration.TotalSeconds);

        if (seconds < 600)
            return $"{seconds / 60}:{seconds % 60:00}";

        var minutes = seconds / 60;

        if (minutes < 600)
            return $"{minutes / 60}h{minutes % 60:00}";

        return $"{Math.Min(minutes / 60, 999)}h";
    }

    /// <summary>
    /// Returns how long an outage lasted for the tooltip: "42 s" under a minute, otherwise the same as the widget.
    /// </summary>
    public static string FormatShort(TimeSpan duration) =>
        duration.TotalSeconds < 60 ? $"{Math.Max(0, (long)duration.TotalSeconds)} s" : FormatClock(duration);

    /// <summary>
    /// Returns how long an outage has lasted as a screen reader should say it, such as "1 minute 42 seconds".
    /// </summary>
    public static string FormatSpoken(TimeSpan duration)
    {
        var seconds = Math.Max(0, (long)duration.TotalSeconds);

        if (seconds < 60)
            return Count(seconds, "second");

        var minutes = seconds / 60;

        if (minutes < 60)
            return Join(Count(minutes, "minute"), seconds % 60, "second");

        return Join(Count(minutes / 60, "hour"), minutes % 60, "minute");
    }

    /// <summary>
    /// Returns a tooltip line for an outage that has ended, such as "Back after 1:42 (2:02 PM–2:04 PM)", with one time when it started and ended in the same minute.
    /// The times are in UTC and shown in local time.
    /// </summary>
    public static string DescribeRecovery(DateTime start, DateTime end, DateTime now)
    {
        var from = TimeText.FormatMoment(start.ToLocalTime(), now.ToLocalTime());
        var to = TimeText.FormatMoment(end.ToLocalTime(), now.ToLocalTime());

        return $"Back after {FormatShort(end - start)} ({(from == to ? to : $"{from}–{to}")})";
    }

    /// <summary>
    /// Returns what the latency reading shows and says, given how the recent pings went.
    /// </summary>
    /// <param name="newestFinished">The newest ping that finished: its round trip time, <see cref="LatencyHistory.Lost" />, or null if none has.</param>
    /// <param name="isWaiting">Whether a newer ping is still waiting for its reply.</param>
    /// <param name="lastRoundtrip">The newest round trip time in the last minute, or null if there's none.</param>
    /// <param name="secondsSinceReply">Whole seconds since the newest second whose ping got a reply, or null if none has since the app started or the PC woke up.</param>
    /// <param name="lastReplyAt">When the last reply arrived, in UTC, or null if none has.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="isReconnecting">Whether the PC only just got its connection back, when pings often fail for a few seconds.</param>
    /// <param name="hasRepliedOnThisNetwork">Whether a ping has ever come back through this network's router.</param>
    public static LatencyView Decide(long? newestFinished, bool isWaiting, long? lastRoundtrip, long? secondsSinceReply, DateTime? lastReplyAt, DateTime now, bool isReconnecting, bool hasRepliedOnThisNetwork)
    {
        if (newestFinished is long roundtrip && roundtrip >= 0 && !isWaiting)
            return LatencyView.Reading(roundtrip);

        if (lastRoundtrip is long recent && now - lastReplyAt < LiveFor)
            return LatencyView.Reading(recent);

        if (isReconnecting)
            return LatencyView.Note("Reconnecting…", "reconnecting");

        // Some networks, like many public Wi-Fi and work networks, block ping; a clock counting up there would only ever cry wolf.
        if (!hasRepliedOnThisNetwork && (newestFinished == LatencyHistory.Lost || secondsSinceReply >= ClockAfterSeconds))
            return LatencyView.BlockedNetwork();

        if (secondsSinceReply is not long seconds)
            return newestFinished == LatencyHistory.Lost
                ? LatencyView.Note("Waiting for a reply", "waiting for a reply")
                : LatencyView.Note("Waiting for the first reply", "waiting for the first reply");

        if (seconds < ClockAfterSeconds)
            return lastRoundtrip is long last ? LatencyView.Dimmed(last) : LatencyView.Note("Waiting for a reply", "waiting for a reply");

        return LatencyView.Clock(TimeSpan.FromSeconds(seconds), lastReplyAt ?? now.AddSeconds(-seconds), now);
    }

    private static string Count(long value, string unit) => value == 1 ? $"1 {unit}" : $"{value} {unit}s";

    private static string Join(string larger, long smaller, string unit) => smaller == 0 ? larger : $"{larger} {Count(smaller, unit)}";
}

/// <summary>
/// What the latency reading shows: the text on the widget, whether it's dimmed, a line for the tooltip, and what a screen reader says.
/// </summary>
public readonly struct LatencyView
{
    private LatencyView(string value, bool isStale, string note, string spoken, bool isOutage, bool isBlockedNetwork = false)
    {
        Value = value;
        IsStale = isStale;
        NoteLine = note;
        Spoken = spoken;
        IsOutage = isOutage;
        IsBlockedNetwork = isBlockedNetwork;
    }

    public string Value { get; }

    public bool IsStale { get; }

    /// <summary>
    /// A line for the tooltip explaining a reading that isn't a number, or null.
    /// </summary>
    public string NoteLine { get; }

    /// <summary>
    /// What a screen reader says after the reading's name.
    /// </summary>
    public string Spoken { get; }

    /// <summary>
    /// Whether the reading is an outage clock.
    /// </summary>
    public bool IsOutage { get; }

    /// <summary>
    /// Whether the network seems to block ping, so time on it isn't an outage.
    /// </summary>
    public bool IsBlockedNetwork { get; }

    /// <summary>
    /// Whether the reading is a reply that just came back.
    /// </summary>
    public bool IsLive => !IsStale && NoteLine is null && !IsOutage;

    public static LatencyView Reading(long roundtrip) =>
        new(LatencyMonitor.FormatRoundtrip(roundtrip), false, null, LatencyMonitor.FormatSpoken(LatencyMonitor.FormatRoundtrip(roundtrip)), false);

    public static LatencyView Dimmed(long roundtrip) =>
        new(LatencyMonitor.FormatRoundtrip(roundtrip), true, "Waiting for a reply", LatencyMonitor.FormatSpoken(LatencyMonitor.FormatRoundtrip(roundtrip)), false);

    public static LatencyView Note(string note, string spoken) => new("—", false, note, spoken, false);

    public static LatencyView BlockedNetwork() =>
        new("—", false, "No replies on this network yet. It may block ping.", "no replies on this network", false, true);

    /// <param name="outage">How long since the last reply.</param>
    /// <param name="lastReplyAt">When the last reply arrived, in UTC.</param>
    /// <param name="now">The current time, in UTC.</param>
    public static LatencyView Clock(TimeSpan outage, DateTime lastReplyAt, DateTime now) =>
        new(Outage.FormatClock(outage), false, $"No reply for {Outage.FormatShort(outage)} (since {TimeText.FormatMoment(lastReplyAt.ToLocalTime(), now.ToLocalTime())})", $"no reply for {Outage.FormatSpoken(outage)}", true);
}
