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
    /// Held around all store work in each public method. The poll loop evaluates off the UI thread and
    /// the tray mutes on it, against the same store, and the store's check, record, and save are
    /// one sequence: guarding each store call alone would still let a mute land between a check
    /// and its record, or modify the set while a save is writing it out.
    /// </summary>
    private readonly object _gate = new();

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
    /// <para>
    /// Per-model caps are never due; see <see cref="Alerts"/>.
    /// </para>
    /// </summary>
    public IReadOnlyList<Alert> TakeNewAlerts(QuotaSnapshot snapshot, Thresholds thresholds)
    {
        lock (_gate)
        {
            var due = new List<Alert>();

            foreach (var window in snapshot.Windows.Where(Alerts))
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
    /// <para>
    /// Does nothing for a per-model cap, which never alerts.
    /// </para>
    /// </summary>
    public void Mute(QuotaWindow window, Thresholds thresholds)
    {
        if (!Alerts(window))
        {
            return;
        }

        lock (_gate)
        {
            foreach (var (threshold, _) in WorstFirst(thresholds))
            {
                if (KeyFor(window, threshold) is { } key)
                {
                    store.MarkFired(key);
                }
            }
        }
    }

    /// <summary>
    /// Whether a window can raise an alert at all. Per-model caps cannot: like the HUD's
    /// severity (quota-hud AC-5), notifications cover the two windows the HUD shows, and a per-model cap
    /// lives in the detail panel (quota-hud AC-23). A toast about one would name a window the HUD has no
    /// line for.
    /// </summary>
    private static bool Alerts(QuotaWindow window) => window.Kind != WindowKind.WeeklyScoped;

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
    ///
    /// <para>
    /// The threshold goes in exactly as the user set it. Rounding it would let two lines that
    /// round alike share one key, and the second crossing would find the first already fired.
    /// </para>
    /// </summary>
    private static AlertKey? KeyFor(QuotaWindow window, double threshold) =>
        AlertKey.For(window, threshold);
}
