namespace BingoHud.Core.Usage;

/// <summary>
/// Which quota window a reading belongs to.
///
/// These are the windows Bingo understands. Anything the endpoint reports that does not map
/// to one of them is ignored rather than guessed at — the payload carries keys for unreleased
/// features, and inventing a window for one of them would put a number on screen that nothing
/// backs.
/// </summary>
public enum WindowKind
{
    /// <summary>The rolling five-hour window. Reported as <c>session</c>.</summary>
    Session,

    /// <summary>The weekly cap covering all models. Reported as <c>weekly_all</c>.</summary>
    WeeklyAll,

    /// <summary>
    /// A weekly cap restricted to one model. Recognized by a non-null <c>scope</c> naming the
    /// model, whatever its <c>kind</c>; the first one observed, on 2026-11-11, reported
    /// <c>weekly_scoped</c>.
    /// </summary>
    WeeklyScoped,
}
