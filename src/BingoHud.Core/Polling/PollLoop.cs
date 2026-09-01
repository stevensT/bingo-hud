using BingoHud.Core.Alerts;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Time;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Polling;

/// <summary>
/// Drives the monitor on a schedule. The only component in Bingo that owns a timer.
///
/// <para>
/// It deliberately decides almost nothing. <see cref="PollPolicy"/> owns the cadence and
/// <see cref="QuotaMonitor"/> owns the backoff, so the loop asks the monitor when it will accept
/// another attempt and waits until then. Recomputing the cadence here would put a second copy of
/// the same rule in a second place, and the two would drift.
/// </para>
/// <para>
/// The signals Core cannot see — battery, whether the panel is open, whether Claude Code is
/// working — come from a delegate the caller supplies, and are gathered immediately before each
/// attempt rather than once at construction. A cadence that exists to react cannot be computed
/// from a snapshot taken at startup.
/// </para>
/// </summary>
public sealed class PollLoop(
    QuotaMonitor monitor,
    IClock clock,
    Func<PollSignals> gatherSignals,
    AlertEngine? alerts = null,
    Func<Thresholds>? thresholds = null,
    Action<IReadOnlyList<Alert>>? onAlerts = null)
{
    /// <summary>
    /// Polls until cancelled, or until the endpoint says this account cannot use it.
    ///
    /// <para>
    /// Cancellation completes normally rather than throwing. Shutdown is not an error, and a
    /// loop that threw its own stop signal back at the caller would earn a catch block at every
    /// call site that could only swallow it.
    /// </para>
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var signals = gatherSignals();

                // Gathering reads the filesystem and the power state, so shutdown can land
                // inside it. Checking again here means a request is never issued after we have
                // been told to stop — the fetch would be aborted in flight anyway, but it would
                // already have been spent against a rate-limited endpoint.
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var result = await monitor
                    .RefreshAsync(signals, cancellationToken)
                    .ConfigureAwait(false);

                HandOverAnyAlerts();

                if (IsTerminal(result))
                {
                    return;
                }

                // Null only before a first attempt has been scheduled, which cannot be reached
                // from here — one has just been made. A non-positive wait means the backoff has
                // already elapsed, so the next pass simply fetches again.
                if (monitor.NextAttemptAt - clock.Now is { } wait && wait > TimeSpan.Zero)
                {
                    await clock.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Asked to stop, mid-fetch or mid-wait. Nothing to report and nothing to clean up.
        }
    }

    /// <summary>
    /// Evaluates the current reading and hands over whatever it is due.
    ///
    /// <para>
    /// Run on every completed pass rather than only after a successful fetch. Deduplication in
    /// <see cref="AlertEngine"/> already makes a repeat evaluation cost nothing, so the simpler
    /// rule is also the more complete one: it catches a threshold crossed by a manual refresh,
    /// whose result the loop never sees and whose backoff will refuse the loop's own next
    /// attempt.
    /// </para>
    /// <para>
    /// Nothing is raised here. The alerts go to whoever is listening — the shell and its toasts
    /// in the app, a notifier alone in a notification-only build — because deciding and
    /// announcing are different jobs and only the first belongs in Core.
    /// </para>
    /// </summary>
    private void HandOverAnyAlerts()
    {
        if (alerts is null || onAlerts is null || monitor.Current.Last is not { } snapshot)
        {
            return;
        }

        var due = alerts.TakeNewAlerts(snapshot, (thresholds ?? (() => Thresholds.Default))());

        if (due.Count > 0)
        {
            onAlerts(due);
        }
    }

    /// <summary>
    /// Whether there is any point asking again.
    ///
    /// <para>
    /// Only <see cref="FetchOutcome.Unsupported"/> qualifies: the endpoint has said it is not
    /// usable on this account, and no amount of waiting changes an account. Notably an
    /// authentication failure does not qualify, even though the monitor freezes the reading for
    /// both. A signed-out user can sign in to Claude Code at any moment, and a loop that had
    /// already stopped would never find out — Bingo would sit there dead until restarted, with
    /// no sign that it had given up.
    /// </para>
    /// </summary>
    private static bool IsTerminal(RefreshResult result) =>
        result is RefreshResult.Performed { State.LastFailure: FetchOutcome.Unsupported };
}
