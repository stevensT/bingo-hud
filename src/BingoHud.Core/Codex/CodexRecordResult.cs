using BingoHud.Core.Usage;

namespace BingoHud.Core.Codex;

/// <summary>
/// What one line of a Codex session log turned out to be.
///
/// <para>
/// Five cases rather than a snapshot that may be null, because four different things can stand
/// where a reading would be, and each is handled differently by the reader. A record whose rate
/// limits are null is Codex saying it has nothing to report: no reading, never 0%. A line that is
/// not a rate-limit record at all is the ordinary case, most of the log. A damaged line is passed
/// over. A rate-limit record in a shape Bingo does not recognise is a sign Codex has changed, and
/// has to be said.
/// </para>
/// </summary>
public abstract record CodexRecordResult
{
    private CodexRecordResult() { }

    /// <summary>
    /// A rate-limit record with at least one window of a length Bingo knows.
    /// </summary>
    /// <param name="Snapshot">
    /// The windows, timed by the record's own timestamp. <see cref="QuotaSnapshot.RawBody"/> is
    /// the <c>rate_limits</c> object exactly as written, and nothing else from the line.
    /// </param>
    /// <param name="ReachedType">
    /// <c>rate_limit_reached_type</c> as written, or null when it is absent or null. Never seen
    /// non-null, so its values are not interpreted; the detail panel shows it as it came.
    /// </param>
    /// <param name="IgnoredWindows">
    /// Windows of a length Bingo does not know, named for the detail panel, as
    /// "secondary (1440 minutes)". Left out of the snapshot rather than guessed into a slot.
    /// </param>
    public sealed record Figures(
        QuotaSnapshot Snapshot,
        string? ReachedType,
        IReadOnlyList<string> IgnoredWindows) : CodexRecordResult;

    /// <summary>A rate-limit record whose <c>rate_limits</c> is null: no reading.</summary>
    public sealed record NoReading : CodexRecordResult;

    /// <summary>A line that is not a rate-limit record. Nothing from it is kept.</summary>
    public sealed record NotARecord : CodexRecordResult;

    /// <summary>
    /// A line that mentions rate limits and is not valid JSON: damage, such as a write cut short
    /// and later finished by the next append, not Codex changing what it writes. It cannot be
    /// dated, so it is passed over and the newest record that can be read wins (codex-quota spec,
    /// Decided, 2026-10-15).
    /// </summary>
    public sealed record Damaged : CodexRecordResult;

    /// <summary>
    /// A rate-limit record that could not be read as one: a field missing or of the wrong type, no
    /// window of a length Bingo knows, or two windows of the same length.
    /// </summary>
    /// <param name="Reason">What was wrong, as a whole sentence, for the detail panel.</param>
    /// <param name="ObservedAt">The record timestamp, when readable, for ordering across logs.</param>
    public sealed record Unreadable(string Reason, DateTimeOffset? ObservedAt = null) : CodexRecordResult;
}
