using BingoHud.Core.Usage;

namespace BingoHud.Core.Alerts;

/// <summary>
/// Decides which alerts a reading is due, and remembers that they were due.
///
/// <para>
/// "Due" is deliberately not "crossed". Comparing a reading against the previous one would say
/// nothing when Bingo starts up already below a threshold, which is the case it most needs to
/// speak up in. Instead a window at or beyond a line is due that alert unless the store says it
/// has already fired — and because <see cref="AlertKey"/> carries the reset time, "already
/// fired" stops being true the moment the window resets.
/// </para>
/// </summary>
public sealed class AlertEngine(IAlertStateStore store)
{
    /// <summary>
    /// The alerts this reading is due. Each is recorded as fired before it is returned, so
    /// calling twice with the same reading yields nothing the second time.
    ///
    /// <para>
    /// A reading that is already past both lines is due only the critical alert, but records the
    /// warning too. Utilization cannot fall within an occurrence, so a warning that has been
    /// overtaken will never be news again — delivering it afterwards would only say something
    /// milder than what the user was just told.
    /// </para>
    /// </summary>
    public IReadOnlyList<Alert> TakeNewAlerts(QuotaSnapshot snapshot, Thresholds thresholds)
    {
        var due = new List<Alert>();

        foreach (var window in snapshot.Windows)
        {
            var remaining = 100 - window.UsedPercent;
            var alreadyDue = false;

            foreach (var (threshold, severity) in WorstFirst(thresholds))
            {
                if (remaining > threshold || KeyFor(window, threshold) is not { } key
                    || store.HasFired(key))
                {
                    continue;
                }

                store.MarkFired(key);

                // The worse line comes first, so anything after it is the overtaken warning:
                // recorded above, but not delivered.
                if (!alreadyDue)
                {
                    due.Add(new Alert(key, severity, window.UsedPercent));
                    alreadyDue = true;
                }
            }
        }

        return due;
    }

    /// <summary>
    /// Silences this window for the rest of its current occurrence, by recording every threshold
    /// as already fired.
    ///
    /// <para>
    /// Muting needs no state of its own: "do not tell me about this window again" and "you have
    /// already told me about this window" are the same instruction. Written this way, a mute
    /// survives a restart and lifts at the next reset for free, because both of those already
    /// belong to the key. It is also why a mute cannot be indefinite — a quota tool that can be
    /// silenced permanently will be silent on the day it matters.
    /// </para>
    /// </summary>
    public void Mute(QuotaWindow window, Thresholds thresholds)
    {
        foreach (var (threshold, _) in WorstFirst(thresholds))
        {
            if (KeyFor(window, threshold) is { } key)
            {
                store.MarkFired(key);
            }
        }
    }

    /// <summary>
    /// The thresholds, most severe first. Order matters: it is what makes a reading past both
    /// lines report the critical one.
    /// </summary>
    private static (double Threshold, Severity Severity)[] WorstFirst(Thresholds thresholds) =>
    [
        (thresholds.CriticalAtRemaining, Severity.Critical),
        (thresholds.WarningAtRemaining, Severity.Warning),
    ];

    /// <summary>
    /// The key for a threshold on a window, or null when the window reports no reset time and so
    /// has no occurrence to be once-per.
    /// </summary>
    private static AlertKey? KeyFor(QuotaWindow window, double threshold) =>
        AlertKey.For(window, (int)Math.Round(threshold));
}
