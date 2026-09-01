namespace BingoHud.Core.Usage;

/// <summary>
/// How much trouble the account is in, as the HUD reports it.
///
/// <para>
/// Three discrete states rather than a gradient. At HUD size a continuously varying colour reads
/// as noise: it is always changing slightly and therefore never says anything. A state that
/// changes three times in a window says something each time it changes.
/// </para>
/// </summary>
public enum Severity
{
    Normal,

    /// <summary>Crossed the warning threshold. 25% remaining by default.</summary>
    Warning,

    /// <summary>Crossed the critical threshold. 10% remaining by default.</summary>
    Critical,

    /// <summary>
    /// The server itself is refusing work against a window. Distinct from
    /// <see cref="Critical"/> on purpose (AC-6): a local threshold is Bingo's own opinion about
    /// a percentage, while this is the service saying no. They call for different reactions and
    /// must not look alike.
    /// </summary>
    RateLimited,
}
