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
            var worstFirst = new[]
            {
                (Threshold: thresholds.CriticalAtRemaining, Severity: Severity.Critical),
                (Threshold: thresholds.WarningAtRemaining, Severity: Severity.Warning),
            };

            var alreadyDue = false;

            foreach (var (threshold, severity) in worstFirst)
            {
                if (remaining > threshold)
                {
                    continue;
                }

                var key = AlertKey.For(window, (int)Math.Round(threshold));
                if (key is null || store.HasFired(key))
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
}
