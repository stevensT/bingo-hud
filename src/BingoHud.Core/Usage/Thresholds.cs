namespace BingoHud.Core.Usage;

/// <summary>
/// Where the warning and critical lines sit, expressed as percentage remaining.
///
/// <para>
/// Stated as remaining rather than consumed because that is how a person thinks about a quota
/// they are about to run out of. Stored values are consumed, so the conversion happens once,
/// inside the policy.
/// </para>
/// </summary>
/// <param name="WarningAtRemaining">Warn at or below this much remaining.</param>
/// <param name="CriticalAtRemaining">Escalate at or below this much remaining.</param>
public sealed record Thresholds(double WarningAtRemaining, double CriticalAtRemaining)
{
    /// <summary>
    /// The defaults from the spec: 25% remaining warns, 10% remaining is critical.
    /// </summary>
    public static Thresholds Default { get; } = new(WarningAtRemaining: 25, CriticalAtRemaining: 10);
}
