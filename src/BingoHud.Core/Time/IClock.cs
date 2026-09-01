namespace BingoHud.Core.Time;

/// <summary>
/// The only source of time inside Core. Nothing in Core reads the system clock or waits on its
/// own.
///
/// This exists so that staleness, reset countdowns, window-occurrence identity, backoff, and
/// the poll cadence are deterministic under test. Those behaviours are the ones most likely to
/// harbour bugs and the hardest to reproduce if they depend on wall-clock time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// The current instant, carrying its UTC offset. The offset is part of the value because
    /// reset times are shown to the user in local time.
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Waits for the given span.
    ///
    /// <para>
    /// Here rather than as a direct <c>Task.Delay</c> call because the poll cadence is measured
    /// in tens of minutes: a loop that waits for real cannot be tested, and a second time
    /// abstraction alongside this one would mean tests could freeze "now" while the code still
    /// slept in real time.
    /// </para>
    /// </summary>
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}
