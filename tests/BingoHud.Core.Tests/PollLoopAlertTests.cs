using BingoHud.Core.Alerts;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The last unconnected wire: readings reaching the alert engine.
///
/// <para>
/// The engine has been able to decide since Phase 5 and nothing has ever asked it. The loop is
/// where that connection belongs, because the loop is the only thing that happens repeatedly —
/// and evaluation is deliberately run on every completed pass rather than only after a
/// successful fetch. Deduplication already makes a repeat evaluation free, so the simpler rule
/// is also the one that catches a threshold crossed by a manual refresh, which the loop never
/// sees the result of.
/// </para>
/// <para>
/// The loop still raises nothing. It hands the decided alerts to whoever is listening, which in
/// the app is the shell and its toasts, and in a notification-only build would be the notifier
/// alone. Alerting is therefore optional here: the schedule is the loop's job, and a loop with
/// nothing attached to it is a legitimate configuration.
/// </para>
/// </summary>
public class PollLoopAlertTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(-7));

    private static readonly DateTimeOffset Reset =
        new(2026, 9, 1, 16, 0, 0, TimeSpan.FromHours(-7));

    private static FetchOutcome Success(DateTimeOffset observedAt, double usedPercent) =>
        new FetchOutcome.Success(new QuotaSnapshot(
            [new QuotaWindow(WindowKind.Session, usedPercent, Reset, ServerSeverity.Normal)],
            observedAt,
            RawBody: "{}"));

    private static QuotaMonitor Monitor(IUsageClient client, TestClock clock) =>
        new(StubCredentialProvider.WithToken(), client, clock);

    private static Func<PollSignals> StopAfter(int iterations, CancellationTokenSource cancellation)
    {
        var gathered = 0;

        return () =>
        {
            if (gathered++ >= iterations)
            {
                cancellation.Cancel();
            }

            return new PollSignals();
        };
    }

    /// <summary>Runs a loop with alerting attached, and returns everything it handed over.</summary>
    private static async Task<List<Alert>> RunCollecting(
        IUsageClient client,
        TestClock clock,
        int iterations,
        AlertEngine? engine = null)
    {
        using var cancellation = new CancellationTokenSource();
        var delivered = new List<Alert>();

        var loop = new PollLoop(
            Monitor(client, clock),
            clock,
            StopAfter(iterations, cancellation),
            engine ?? new AlertEngine(new InMemoryAlertStateStore()),
            () => Thresholds.Default,
            due => delivered.AddRange(due));

        await loop.RunAsync(cancellation.Token);

        return delivered;
    }

    [Fact]
    public async Task AReadingPastAThresholdIsHandedOverAsAnAlert()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now, usedPercent: 95));

        var delivered = await RunCollecting(client, clock, iterations: 1);

        var alert = Assert.Single(delivered);
        Assert.Equal(Severity.Critical, alert.Severity);
        Assert.Equal(WindowKind.Session, alert.Key.Kind);
    }

    [Fact]
    public async Task AHealthyReadingHandsOverNothing()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now, usedPercent: 40));

        Assert.Empty(await RunCollecting(client, clock, iterations: 3));
    }

    [Fact]
    public async Task TheSameThresholdIsNotHandedOverOnEveryPass()
    {
        // Twelve polls of the same unchanged window is one alert, not twelve. Without this the
        // loop would turn a single crossing into a toast every few minutes until the reset.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now, usedPercent: 95));

        Assert.Single(await RunCollecting(client, clock, iterations: 12));
    }

    [Fact]
    public async Task AWorseningWindowRaisesTheSecondThresholdOnALaterPass()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(call =>
            Success(clock.Now, usedPercent: call == 0 ? 80 : 95));

        var delivered = await RunCollecting(client, clock, iterations: 3);

        Assert.Equal(2, delivered.Count);
        Assert.Equal(Severity.Warning, delivered[0].Severity);
        Assert.Equal(Severity.Critical, delivered[1].Severity);
    }

    [Fact]
    public async Task AThresholdCrossedByAManualRefreshIsPickedUpOnTheNextPass()
    {
        // The loop never sees the result of a refresh it did not ask for, and its own next
        // attempt will be refused by the backoff. Evaluating the current reading every pass
        // rather than only after a fetch is what stops that alert from being lost.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now, usedPercent: 95));
        var monitor = Monitor(client, clock);
        using var cancellation = new CancellationTokenSource();
        var delivered = new List<Alert>();

        await monitor.RefreshAsync(new PollSignals());

        var loop = new PollLoop(
            monitor,
            clock,
            StopAfter(1, cancellation),
            new AlertEngine(new InMemoryAlertStateStore()),
            () => Thresholds.Default,
            due => delivered.AddRange(due));

        await loop.RunAsync(cancellation.Token);

        Assert.Single(delivered);
        Assert.Equal(1, client.Fetches);
    }

    [Fact]
    public async Task NoReadingAtAllHandsOverNothing()
    {
        // A failure before any success has ever landed. There is no number, so there is nothing
        // that could be past a threshold.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Transient(RetryAfter: null));

        Assert.Empty(await RunCollecting(client, clock, iterations: 3));
    }

    [Fact]
    public async Task AFailureAfterAnAlertDoesNotRepeatIt()
    {
        // The reading survives failures, and the loop evaluates it on every pass. Without
        // deduplication that combination would re-announce the same crossing for as long as the
        // endpoint stayed down — loudest exactly when Bingo knows least.
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start, usedPercent: 95),
            new FetchOutcome.Transient(RetryAfter: null));

        Assert.Single(await RunCollecting(client, clock, iterations: 5));
    }

    [Fact]
    public async Task ALoopWithNothingListeningStillPolls()
    {
        // One outcome of the spike gate is a notification-only tool, and another is no HUD at
        // all. The schedule is the loop's job either way, so alerting is attached, not built in.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now, usedPercent: 95));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(2, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal(2, client.Fetches);
    }

    [Fact]
    public async Task TheThresholdsAreReadFreshlyEachTimeRatherThanCapturedOnce()
    {
        // Thresholds are a user setting. One edited mid-session must take effect on the next
        // pass, not at the next restart.
        var clock = new TestClock(Start);

        // 70% consumed is 30% remaining: inside the default warning line, outside a widened one.
        var client = new StubUsageClient(_ => Success(clock.Now, usedPercent: 70));
        using var cancellation = new CancellationTokenSource();
        var delivered = new List<Alert>();
        var thresholds = Thresholds.Default;
        var gathered = 0;

        var loop = new PollLoop(
            Monitor(client, clock),
            clock,
            () =>
            {
                if (gathered == 1)
                {
                    thresholds = new Thresholds(WarningAtRemaining: 40, CriticalAtRemaining: 10);
                }

                if (gathered >= 2)
                {
                    cancellation.Cancel();
                }

                gathered++;

                return new PollSignals();
            },
            new AlertEngine(new InMemoryAlertStateStore()),
            () => thresholds,
            due => delivered.AddRange(due));

        await loop.RunAsync(cancellation.Token);

        // Silent on the first pass under the default, due on the second under the widened one.
        Assert.Single(delivered);
        Assert.Equal(40, delivered[0].Key.ThresholdPercent);
    }
}
