namespace BingoHud.Core.Usage;

/// <summary>
/// How serious the server says a window is, as reported per limit.
///
/// <para>
/// Only <c>normal</c> has ever been observed. The other spellings are inferred from prior art,
/// not seen, which is exactly why <see cref="Unknown"/> exists and why an unrecognized string
/// maps to it. Rounding an unfamiliar severity down to <see cref="Normal"/> would mean the one
/// case this enum exists to catch — the server escalating in a spelling nobody anticipated —
/// silently reads as everything being fine.
/// </para>
/// <para>
/// <see cref="Unknown"/> also covers a window that reports no severity at all, which is every
/// window on the flat fallback path: that form carries no severity field. Both cases mean the
/// same thing to everything downstream — the server's opinion is unavailable, so only local
/// thresholds apply.
/// </para>
/// </summary>
public enum ServerSeverity
{
    /// <summary>Not reported, or reported in a spelling this version does not recognize.</summary>
    Unknown,

    /// <summary>The only value observed on a live response.</summary>
    Normal,

    Warning,

    Critical,

    /// <summary>The server is refusing work against this window.</summary>
    Rejected,
}
