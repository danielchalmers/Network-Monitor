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
    public void Complete(Ticket ticket, long? roundtripTime)
    {
        lock (_lock)
        {
            var index = ticket.Sequence - _firstSequence;

            if (ticket.Generation == _generation && index >= 0 && index < _slots.Count)
                _slots[(int)index] = roundtripTime ?? Lost;
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
