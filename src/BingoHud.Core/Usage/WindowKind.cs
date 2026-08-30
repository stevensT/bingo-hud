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
    /// A weekly cap restricted to one model. Reported with a non-null <c>scope</c>. Never yet
    /// observed on a live account, so its empty state is the common case.
    /// </summary>
    WeeklyScoped,
}
