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
    // No accent for the HUD as a whole. Each product's section carries its own, its worst line
    // (HudContent.Reading.Overall), drawn beside that product's lines only: one accent down the
    // whole HUD read as both products being in trouble when only one was (task 5.4).

    /// <summary>
    /// Whether this would draw the same HUD as <paramref name="other"/>, so a repaint can be
    /// skipped. Written out because a record compares a list member by reference.
    /// </summary>
    public bool SameAs(Hud other) =>
        Sections.Count == other.Sections.Count
        && Sections.Zip(other.Sections).All(pair =>
            pair.First.Product == pair.Second.Product && pair.First.Content.SameAs(pair.Second.Content));
}
