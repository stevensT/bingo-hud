namespace BingoHud.Core.Time;

/// <summary>
/// The only source of "now" inside Core. Nothing in Core reads the system clock directly.
///
/// This exists so that staleness, reset countdowns, window-occurrence identity, and backoff
/// are deterministic under test. Those behaviours are the ones most likely to harbour bugs
/// and the hardest to reproduce if they depend on wall-clock time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// The current instant, carrying its UTC offset. The offset is part of the value because
    /// reset times are shown to the user in local time.
    /// </summary>
    DateTimeOffset Now { get; }
}
