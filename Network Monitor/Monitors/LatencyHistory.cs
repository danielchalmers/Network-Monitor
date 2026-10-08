using System;
using System.Collections.Generic;
using System.Linq;

namespace Network_Monitor.Monitors;

/// <summary>
/// One slot per second for the last minute, each holding how that second's ping turned out.
/// Pings complete on other threads and sometimes out of order, so each one is filed under the second it was sent in rather than whenever its reply arrives.
/// </summary>
public sealed class LatencyHistory
{
    /// <summary>
    /// A slot whose ping hasn't come back or timed out yet.
    /// </summary>
    public const long Pending = -2;

    /// <summary>
    /// A slot whose ping got no reply.
    /// </summary>
    public const long Lost = -1;

    private readonly int _capacity;
    private readonly List<long> _slots = new();
    private readonly object _lock = new();
    private long _firstSequence;
    private int _generation;
    private DateTime? _lastReplyAt;
    private long? _lastReplySequence;

    /// <param name="seconds">How many finished seconds to keep. One more slot is kept for the second whose ping is still out.</param>
    public LatencyHistory(int seconds)
    {
        _capacity = seconds + 1;
    }

    /// <summary>
    /// Opens the slot for a new second and returns the ticket its ping reports back with.
    /// </summary>
    public Ticket Open()
    {
        lock (_lock)
        {
            _slots.Add(Pending);

            while (_slots.Count > _capacity)
            {
                _slots.RemoveAt(0);
                _firstSequence++;
            }

            return new Ticket(_firstSequence + _slots.Count - 1, _generation);
        }
    }

    /// <summary>
    /// Records how a ping turned out: its round trip time in milliseconds, or null if it got no reply.
    /// Ignored if its second has since fallen out of the window, or the history was reset after it was sent.
    /// </summary>
    public void Complete(Ticket ticket, long? roundtripTime) => Complete(ticket, roundtripTime, DateTime.UtcNow);

    /// <inheritdoc cref="Complete(Ticket, long?)" />
    /// <param name="ticket">The ticket from when the ping was sent.</param>
    /// <param name="roundtripTime">The round trip time in milliseconds, or null if there was no reply.</param>
    /// <param name="at">When the ping finished, in UTC.</param>
    public void Complete(Ticket ticket, long? roundtripTime, DateTime at)
    {
        lock (_lock)
        {
            var index = ticket.Sequence - _firstSequence;

            if (ticket.Generation != _generation || index < 0 || index >= _slots.Count)
                return;

            _slots[(int)index] = roundtripTime ?? Lost;

            if (roundtripTime.HasValue)
            {
                _lastReplyAt = at;

                if (!(_lastReplySequence >= ticket.Sequence))
                    _lastReplySequence = ticket.Sequence;
            }
        }
    }

    /// <summary>
    /// Returns the slots and when replies last came back, all from the same moment.
    /// Call it before <see cref="Open" /> on each tick, so the seconds since a reply count up from the second whose ping got it.
    /// </summary>
    public Snapshot GetSnapshot()
    {
        lock (_lock)
        {
            var nextSequence = _firstSequence + _slots.Count;
            return new Snapshot(_slots.ToArray(), _lastReplyAt, nextSequence - _lastReplySequence);
        }
    }

    /// <summary>
    /// Forgets every slot, and any ping still out, such as after the PC wakes up.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _firstSequence += _slots.Count;
            _slots.Clear();
            _generation++;
            _lastReplyAt = null;
            _lastReplySequence = null;
        }
    }

    /// <summary>
    /// Returns every slot, oldest first: a round trip time, <see cref="Lost" />, or <see cref="Pending" />.
    /// </summary>
    public long[] GetSlots()
    {
        lock (_lock)
            return _slots.ToArray();
    }

    /// <summary>
    /// Returns the seconds whose pings have finished, oldest first: a round trip time or <see cref="Lost" />.
    /// </summary>
    public static long[] GetFinished(IEnumerable<long> slots) => slots.Where(x => x != Pending).ToArray();

    /// <summary>
    /// The slots and the latest reply at one moment.
    /// </summary>
    public readonly struct Snapshot
    {
        public Snapshot(long[] slots, DateTime? lastReplyAt, long? secondsSinceReply)
        {
            Slots = slots;
            LastReplyAt = lastReplyAt;
            SecondsSinceReply = secondsSinceReply;
        }

        /// <summary>
        /// Every slot, oldest first: a round trip time, <see cref="Lost" />, or <see cref="Pending" />.
        /// </summary>
        public long[] Slots { get; }

        /// <summary>
        /// When the last reply arrived, in UTC, or null if none has since the history started or was reset.
        /// </summary>
        public DateTime? LastReplyAt { get; }

        /// <summary>
        /// Whole seconds since the newest second whose ping got a reply, counted in clock ticks so the outage clock never skips or repeats a second.
        /// Null if none has since the history started or was reset.
        /// </summary>
        public long? SecondsSinceReply { get; }
    }

    /// <summary>
    /// Identifies the second a ping was sent in.
    /// </summary>
    public readonly struct Ticket
    {
        public Ticket(long sequence, int generation)
        {
            Sequence = sequence;
            Generation = generation;
        }

        public long Sequence { get; }

        public int Generation { get; }
    }
}
