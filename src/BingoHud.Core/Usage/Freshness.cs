namespace BingoHud.Core.Usage;

/// <summary>
/// How much a reading can still be trusted as current.
/// </summary>
public enum Freshness
{
    /// <summary>Recent enough to be taken at face value.</summary>
    Fresh,

    /// <summary>Old enough that its age is shown alongside it (AC-8).</summary>
    Stale,

    /// <summary>
    /// It can no longer be refreshed at all — the credential is gone or the endpoint is
    /// unreachable. The reading stays on screen, marked, but is excluded from severity so that
    /// a dead reading cannot own the headline (AC-13).
    /// </summary>
    Frozen,
}
