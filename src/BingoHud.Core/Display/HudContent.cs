namespace BingoHud.Core.Display;

/// <summary>
/// Everything on the HUD at one instant (AC-1 through AC-3, AC-7 through AC-10).
///
/// <para>
/// Two cases rather than one shape with nullable halves, because the HUD really has two states
/// and the difference between them decides what gets drawn. Written as a closed hierarchy so the
/// compiler will not let a caller produce lines without deciding what to say when there are none:
/// an earlier version left that to a nullable field, and a successful response carrying only
/// per-model caps then produced no lines and no words — a HUD with nothing written on it, which
/// looks exactly like a HUD that has crashed.
/// </para>
/// </summary>
public abstract record HudContent
{
    private HudContent() { }

    /// <summary>
    /// Numbers to draw, and how much they can still be trusted.
    /// </summary>
    /// <param name="Lines">One per window shown, session first. Never empty.</param>
    /// <param name="Mark">
    /// What the reading as a whole is worth: its age, and the reason it stopped refreshing when
    /// there is one. Null while the reading is current. Held once here rather than on each line,
    /// because it is a fact about the reading and not about any one window — repeating it down
    /// the display with the least room to spare is exactly what
    /// <see cref="Readout.Content"/> refuses to do with the headline.
    /// </param>
    public sealed record Reading(IReadOnlyList<ReadoutLine> Lines, string? Mark) : HudContent;

    /// <summary>
    /// No numbers, and the words that stand in for them.
    /// </summary>
    /// <param name="Phrase">
    /// Why there is nothing to show. Never null and never empty: this case exists precisely so
    /// that an empty HUD always says something.
    /// </param>
    public sealed record Empty(string Phrase) : HudContent;

    /// <summary>
    /// Whether this would draw the same HUD as <paramref name="other"/>, so a repaint can be
    /// skipped.
    ///
    /// <para>
    /// Written out rather than left to the record's own equality, and it has to be. A record
    /// compares its members with the default comparer, and for an <see cref="IReadOnlyList{T}"/>
    /// member that is reference equality — so <c>==</c> on two Readings holding identical lines
    /// is false, and a shell trusting it would rebuild its visual tree every second. It lives
    /// here rather than in the shell so that it is covered by tests; the WPF layer has none.
    /// </para>
    /// </summary>
    public bool SameAs(HudContent other) => (this, other) switch
    {
        (Reading a, Reading b) => a.Mark == b.Mark && a.Lines.SequenceEqual(b.Lines),
        (Empty a, Empty b) => a.Phrase == b.Phrase,
        _ => false,
    };
}
