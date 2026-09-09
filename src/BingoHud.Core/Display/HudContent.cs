namespace BingoHud.Core.Display;

/// <summary>
/// Everything on the HUD at one instant (AC-1 through AC-3, AC-7 through AC-10).
///
/// <para>
/// Both halves travel together so the shell can compare what it holds against what it has
/// already drawn in one step. Kept apart, an empty HUD whose reason changed would compare equal
/// to itself and never repaint.
/// </para>
/// </summary>
/// <param name="Lines">One per window shown, session first. Empty until the first reading.</param>
/// <param name="EmptyState">
/// The words shown in place of the lines when there are none, or null when there are some. Never
/// null when <see cref="Lines"/> is empty: an empty HUD with nothing written on it reads as a
/// broken one.
/// </param>
public sealed record HudContent(IReadOnlyList<ReadoutLine> Lines, string? EmptyState);
