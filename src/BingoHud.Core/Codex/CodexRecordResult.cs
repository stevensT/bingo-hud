using BingoHud.Core.Usage;

namespace BingoHud.Core.Codex;

/// <summary>
/// What one line of a Codex session log turned out to be.
///
/// <para>
/// Four cases rather than a snapshot that may be null, because three different things can stand
/// where a reading would be, and each is handled differently by the reader. A record whose rate
/// limits are null is Codex saying it has nothing to report: no reading, never 0%. A line that is
/// not a rate-limit record at all is the ordinary case, most of the log. A rate-limit record in a
/// shape Bingo does not recognise is a sign Codex has changed, and has to be said.
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
    /// <c>rate_limit_reached_type</c> exactly as written, or null when Codex sent null. Never seen
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
    /// A line that should have been a rate-limit record and could not be read as one: not valid
    /// JSON, or a record with a field missing or of the wrong type.
    /// </summary>
    /// <param name="Reason">What was wrong, as a whole sentence, for the detail panel.</param>
    /// <param name="ObservedAt">The record timestamp, when readable, for ordering across logs.</param>
    public sealed record Unreadable(string Reason, DateTimeOffset? ObservedAt = null) : CodexRecordResult;
}
