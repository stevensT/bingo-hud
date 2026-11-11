namespace BingoHud.Core.Usage;

/// <summary>
/// Which quota window a reading belongs to.
///
/// These are the windows Bingo understands. Anything the endpoint reports that does not map
/// to one of them is ignored rather than guessed at — the payload carries keys for unreleased
/// features, and inventing a window for one of them would put a number on screen that nothing
/// backs. Ignoring is for kinds Bingo does not know. A per-model scope in a shape Bingo does not
/// know, or a <c>weekly_scoped</c> entry naming no model, is rejected instead, because ignoring
/// either could drop a real cap or misread it as one of the windows above.
/// </summary>
public enum WindowKind
{
    /// <summary>The rolling five-hour window. Reported as <c>session</c>.</summary>
    Session,

    /// <summary>The weekly cap covering all models. Reported as <c>weekly_all</c>.</summary>
    WeeklyAll,

    /// <summary>
    /// A weekly cap restricted to one model. Recognized by a non-null <c>scope</c> naming the
    /// model, whatever its <c>kind</c>. The first one observed, on 2026-11-11, reported
    /// <c>weekly_scoped</c>; an entry of that kind with no scope is unreadable.
    /// </summary>
    WeeklyScoped,
}
