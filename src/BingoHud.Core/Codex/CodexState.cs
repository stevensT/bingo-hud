using BingoHud.Core.Usage;

namespace BingoHud.Core.Codex;

/// <summary>
/// Whether Bingo has found Codex on this machine.
/// </summary>
public enum CodexPresence
{
    /// <summary>The logs have not been looked at yet. True only before the first scan.</summary>
    NotLookedYet,

    /// <summary>There is no Codex folder for this user.</summary>
    NotFound,

    /// <summary>The Codex folder is there.</summary>
    Found,
}

/// <summary>
/// What the display binds to for Codex (codex-quota AC-1, AC-3, AC-3a, AC-7).
///
/// <para>
/// The Codex counterpart of <see cref="Monitoring.ReadingState"/>, kept separate because the two
/// sources fail in different ways: Claude's through a sign-in and a network, Codex's through a
/// folder and a file format.
/// </para>
/// </summary>
/// <param name="Presence">Whether Codex was found.</param>
/// <param name="Last">
/// The newest record with figures, or null until one has been found. Kept across a failed scan,
/// so a true figure that has merely gone unconfirmed stays on screen, marked.
/// </param>
/// <param name="Problem">
/// Why the last scan gave nothing usable, or null when it was read cleanly. A reading with no
/// problem is idle when it is old: Codex has not been used since. A reading with a problem is
/// failed: Bingo cannot tell whether Codex has been used since (AC-3a).
/// </param>
/// <param name="Age">How old <paramref name="Last"/> is, from its own timestamp, or zero without one.</param>
/// <param name="ReachedType">The reading's <c>rate_limit_reached_type</c> as written, or null.</param>
/// <param name="IgnoredWindows">Windows in the reading of a length Bingo does not know, named.</param>
public sealed record CodexState(
    CodexPresence Presence,
    QuotaSnapshot? Last,
    CodexProblem? Problem,
    TimeSpan Age,
    string? ReachedType,
    IReadOnlyList<string> IgnoredWindows);
