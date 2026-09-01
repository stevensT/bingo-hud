namespace BingoHud.Core.Alerts;

/// <summary>
/// Remembers which alerts have already fired, so each one arrives at most once per window
/// occurrence — including across a restart.
///
/// <para>
/// Nothing here needs to know what a threshold means or when a window resets. Both are already
/// inside <see cref="AlertKey"/>, which is what keeps this a set-membership question rather than
/// a second copy of the alerting rules.
/// </para>
/// </summary>
public interface IAlertStateStore
{
    /// <summary>Whether this exact alert has already fired.</summary>
    bool HasFired(AlertKey key);

    /// <summary>Records that this alert has fired and must not fire again.</summary>
    void MarkFired(AlertKey key);

    /// <summary>
    /// Forgets alerts for windows that reset before the given instant, so the record does not
    /// accumulate occurrences that can never recur.
    /// </summary>
    void Prune(DateTimeOffset before);
}
