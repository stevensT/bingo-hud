using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// One product's part of the HUD: its lines and mark, or the phrase that stands in for them
/// (codex-quota AC-4, AC-7, AC-7a).
/// </summary>
/// <param name="Product">Whose windows these are. The shell labels an empty section with it.</param>
/// <param name="Content">The section's lines and mark, or its phrase.</param>
public sealed record HudSection(Product Product, HudContent Content);

/// <summary>
/// Everything on the HUD at one instant, one section per product shown.
/// </summary>
/// <param name="Sections">One per product switched on, Claude first. Never empty.</param>
public sealed record Hud(IReadOnlyList<HudSection> Sections)
{
    /// <summary>
    /// The worst line across every section, which the shell draws as the accent bar: one glance
    /// says whether anything is in trouble. A frozen reading's lines are already normal, so a
    /// reading that can no longer update cannot own it.
    /// </summary>
    public Severity Overall => Sections
        .Select(section => section.Content is HudContent.Reading reading ? reading.Overall : Severity.Normal)
        .DefaultIfEmpty(Severity.Normal)
        .MaxBy(Readout.Rank);

    /// <summary>
    /// Whether this would draw the same HUD as <paramref name="other"/>, so a repaint can be
    /// skipped. Written out because a record compares a list member by reference.
    /// </summary>
    public bool SameAs(Hud other) =>
        Sections.Count == other.Sections.Count
        && Sections.Zip(other.Sections).All(pair =>
            pair.First.Product == pair.Second.Product && pair.First.Content.SameAs(pair.Second.Content));
}
