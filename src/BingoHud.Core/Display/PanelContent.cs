namespace BingoHud.Core.Display;

/// <summary>
/// One row of the detail panel: a window or a per-model cap, and what is known about it.
/// </summary>
/// <param name="Label">
/// What this row is about: "5h" and "Week" for the two windows the HUD shows, and for a
/// per-model cap the server's own scope string, unaltered.
/// </param>
/// <param name="Percent">
/// The figure and the word that says which way it reads, exactly as the HUD phrases it. The
/// panel and the HUD must never describe the same window two different ways.
/// </param>
/// <param name="Reset">
/// When this window resets, written out in full, or the words for a window that reported no
/// reset time. Never null and never a guess: the panel always says something here, because a
/// blank cell reads as a value that failed to load.
/// </param>
public sealed record PanelRow(string Label, string Percent, string Reset);

/// <summary>
/// Everything the detail panel displays (AC-23, AC-24).
///
/// <para>
/// The panel answers the questions the HUD is too small to: which numbers these are, how old
/// they are, when they were fetched, when the next fetch is due, and which build is doing the
/// reading. It is also the only place a stale or frozen reading is shown as a number, because
/// the HUD blanks those and "why is the HUD empty" has to be answerable somewhere.
/// </para>
/// </summary>
/// <param name="Windows">The two windows the HUD shows, session first. Empty before the first reading.</param>
/// <param name="PerModelCaps">Weekly caps restricted to one model. Empty on every payload observed so far.</param>
/// <param name="PerModelCapsEmptyState">
/// What to show in place of the per-model rows when there are none, or null when there are some.
/// Not an afterthought: this is the state nearly every user will see.
/// </param>
/// <param name="Age">How old the displayed reading is, or null before the first one.</param>
/// <param name="LastPoll">When the last successful poll happened, written out in full (AC-24).</param>
/// <param name="NextPoll">Why the next poll is scheduled when it is.</param>
/// <param name="Version">The running build (AC-24).</param>
public sealed record PanelContent(
    IReadOnlyList<PanelRow> Windows,
    IReadOnlyList<PanelRow> PerModelCaps,
    string? PerModelCapsEmptyState,
    string? Age,
    string LastPoll,
    string NextPoll,
    string Version);
