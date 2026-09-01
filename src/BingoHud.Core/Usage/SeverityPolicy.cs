namespace BingoHud.Core.Usage;

/// <summary>
/// Turns a snapshot into the one severity the HUD reports.
///
/// <para>
/// A pure function over the reading, the user's thresholds, and how much the reading can still
/// be trusted. It reads no clock and holds no state, so every rule below is a table row rather
/// than a behaviour that has to be reproduced.
/// </para>
/// </summary>
public static class SeverityPolicy
{
    /// <summary>
    /// The overall severity of a reading.
    ///
    /// <para>
    /// The worst window wins. A session window at 5% remaining is the user's problem whatever
    /// the weekly window says, and averaging the two would produce a number describing nothing.
    /// </para>
    /// </summary>
    public static Severity Evaluate(
        QuotaSnapshot snapshot,
        Thresholds thresholds,
        Freshness freshness)
    {
        if (freshness == Freshness.Frozen)
        {
            // AC-13. The reading stays on screen, marked and carrying its age, but it cannot
            // drive the headline: the numbers may be hours old, and acting on them is precisely
            // what freezing exists to prevent. Only Frozen is excluded — a stale reading is old
            // but still refreshable, and suppressing it would hide a real problem.
            return Severity.Normal;
        }

        var worst = Severity.Normal;

        foreach (var window in snapshot.Windows)
        {
            worst = Worst(worst, Worst(FromThresholds(window, thresholds), FromServer(window)));
        }

        return worst;
    }

    /// <summary>
    /// Bingo's own opinion about a percentage, from the user's thresholds. Stored values are
    /// consumed and the thresholds are stated as remaining, so the one conversion lives here.
    /// </summary>
    private static Severity FromThresholds(QuotaWindow window, Thresholds thresholds)
    {
        var remaining = 100 - window.UsedPercent;

        if (remaining <= thresholds.CriticalAtRemaining)
        {
            return Severity.Critical;
        }

        return remaining <= thresholds.WarningAtRemaining ? Severity.Warning : Severity.Normal;
    }

    /// <summary>
    /// What the server said about this window.
    ///
    /// <para>
    /// Only ever raises the result, never lowers it — see <see cref="Worst"/>. A server saying
    /// "normal" about a window at 95% consumed does not make it fine, and a server escalating a
    /// window that looks healthy knows something Bingo does not.
    /// </para>
    /// <para>
    /// Only <c>normal</c> has ever been observed on a live response, so every branch below
    /// except that one is inference. <see cref="ServerSeverity.Unknown"/> contributes nothing,
    /// which leaves the local threshold in charge.
    /// </para>
    /// </summary>
    private static Severity FromServer(QuotaWindow window) => window.Severity switch
    {
        ServerSeverity.Rejected => Severity.RateLimited,
        ServerSeverity.Critical => Severity.Critical,
        ServerSeverity.Warning => Severity.Warning,
        _ => Severity.Normal,
    };

    /// <summary>
    /// The more serious of two severities.
    ///
    /// <para>
    /// <see cref="Severity.RateLimited"/> outranks everything, because the service refusing work
    /// is a fact rather than an opinion about a percentage. It is a distinct state rather than a
    /// louder critical (AC-6): the two call for different reactions, so they must not look
    /// alike.
    /// </para>
    /// </summary>
    private static Severity Worst(Severity first, Severity second)
    {
        if (first == Severity.RateLimited || second == Severity.RateLimited)
        {
            return Severity.RateLimited;
        }

        return first > second ? first : second;
    }
}
