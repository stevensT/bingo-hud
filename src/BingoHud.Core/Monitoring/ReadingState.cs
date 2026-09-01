using BingoHud.Core.Usage;

namespace BingoHud.Core.Monitoring;

/// <summary>
/// What the shell binds to. Exactly one of these is current at any time.
///
/// <para>
/// The last successful snapshot is kept across failures on purpose, and so is its age. A reading
/// that is twenty minutes old and labelled as such is useful; the same reading presented as
/// current is a lie, and no reading at all where one used to be looks like a bug. The age is
/// what makes the difference, which is why it is part of the state rather than something the
/// shell works out.
/// </para>
/// </summary>
/// <param name="Last">
/// The most recent successful reading, or null until the first one arrives. Never a placeholder,
/// never a zero.
/// </param>
/// <param name="Freshness">How much this reading can still be trusted as current.</param>
/// <param name="LastFailure">
/// What went wrong most recently, or null if nothing has. Present alongside <see cref="Last"/>
/// when a failure follows a success — both facts are true at once and the display needs both.
/// </param>
/// <param name="Age">How old <see cref="Last"/> is, or zero when there is no reading yet.</param>
/// <param name="PollReason">Why the next poll is scheduled when it is, shown in the panel.</param>
public sealed record ReadingState(
    QuotaSnapshot? Last,
    Freshness Freshness,
    FetchOutcome? LastFailure,
    TimeSpan Age,
    string PollReason);
