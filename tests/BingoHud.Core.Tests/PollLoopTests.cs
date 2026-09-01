using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The thing that finally makes Bingo do something.
///
/// <para>
/// Everything before this phase could fetch, parse, judge and alert, but nothing called any of
/// it. The loop is the only component that owns a schedule, and it owns as little else as
/// possible: it does not decide the cadence, because <see cref="PollPolicy"/> already does, and
/// it does not merge the outcome of a fetch back into the signals, because
/// <see cref="QuotaMonitor"/> already does. It asks the monitor when to come back, and comes
/// back then.
/// </para>
/// <para>
/// The signals it cannot know — battery, whether a panel is open, whether Claude Code is working
/// — are gathered through a delegate the caller supplies, immediately before each attempt. Core
/// cannot read any of them, and gathering them once at construction would freeze a cadence that
/// exists precisely to react.
/// </para>
/// </summary>
public class PollLoopTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(-7));

    private static FetchOutcome Success(DateTimeOffset observedAt) =>
        new FetchOutcome.Success(new QuotaSnapshot(
            [new QuotaWindow(WindowKind.Session, 12, null, ServerSeverity.Normal)],
            observedAt,
            RawBody: "{}"));

    private static QuotaMonitor Monitor(IUsageClient client, TestClock clock) =>
        new(StubCredentialProvider.WithToken(), client, clock);

    /// <summary>
    /// Gathers the same signals every time, and cancels once it has been asked often enough.
    /// Cancelling from here rather than from a timer is what keeps these tests deterministic:
    /// the loop stops after an exact number of completed iterations, never a racy one.
    /// </summary>
    private static Func<PollSignals> StopAfter(
        int iterations,
        CancellationTokenSource cancellation,
        PollSignals? signals = null)
    {
        var gathered = 0;

        return () =>
        {
            if (gathered++ >= iterations)
            {
                cancellation.Cancel();
            }

            return signals ?? new PollSignals();
        };
    }

    [Fact]
    public async Task TheLoopKeepsFetchingUntilItIsCancelled()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(3, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal(3, client.Fetches);
    }

    [Fact]
    public async Task CancellationEndsTheLoopWithoutThrowing()
    {
        // The app cancels this on shutdown. A loop that threw on its own stop signal would make
        // every caller wrap it in a catch that says nothing.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(1, cancellation));

        var run = loop.RunAsync(cancellation.Token);
        await run;

        Assert.True(run.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task AnIdleMachineIsPolledAtTheSlowestCadence()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(2, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal([PollPolicy.Ceiling, PollPolicy.Ceiling], clock.RequestedDelays);
    }

    [Fact]
    public async Task AnOpenPanelBringsTheLoopBackAtTheFloor()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now));
        using var cancellation = new CancellationTokenSource();
        var watching = new PollSignals(SinceUserOpenedPanel: TimeSpan.Zero);
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(2, cancellation, watching));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal([PollPolicy.Floor, PollPolicy.Floor], clock.RequestedDelays);
    }

    [Fact]
    public async Task AFailedFetchDoesNotEndTheLoop()
    {
        // The backoff row of the cadence table only means anything if the loop is still running
        // to obey it. A loop that died on the first network blip would need a restart to recover.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Transient(RetryAfter: null));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(3, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal(3, client.Fetches);
        Assert.Equal(
            [PollPolicy.Ceiling, PollPolicy.Ceiling, PollPolicy.Ceiling],
            clock.RequestedDelays);
    }

    [Fact]
    public async Task AServerRetryAfterSetsTheNextWait()
    {
        var clock = new TestClock(Start);
        var retryAfter = TimeSpan.FromMinutes(11);
        var client = new StubUsageClient(new FetchOutcome.Transient(retryAfter));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(1, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal([retryAfter], clock.RequestedDelays);
    }

    [Fact]
    public async Task ASignedOutUserIsStillPolledForSoThatSigningInIsNoticed()
    {
        // Frozen, but not hopeless: Claude Code can sign in at any moment and the reading would
        // start working again. Stopping here would leave Bingo dead until it was restarted.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(3, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal(3, client.Fetches);
    }

    [Fact]
    public async Task TheLoopStopsWhenTheEndpointIsUnusableOnThisAccount()
    {
        // The one terminal outcome. Nothing about the account changes by asking again, so the
        // loop ends rather than spending a request an hour on something that keeps saying no.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Unsupported(StatusCode: 403));
        using var cancellation = new CancellationTokenSource();
        var loop = new PollLoop(Monitor(client, clock), clock, StopAfter(5, cancellation));

        await loop.RunAsync(cancellation.Token);

        Assert.Equal(1, client.Fetches);
        Assert.False(cancellation.IsCancellationRequested);
    }

    [Fact]
    public async Task SignalsAreGatheredFreshlyBeforeEachAttempt()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(_ => Success(clock.Now));
        using var cancellation = new CancellationTokenSource();
        var gathered = 0;

        var loop = new PollLoop(Monitor(client, clock), clock, () =>
        {
            if (gathered++ >= 3)
            {
                cancellation.Cancel();
            }

            return new PollSignals();
        });

        await loop.RunAsync(cancellation.Token);

        Assert.Equal(4, gathered);
        Assert.Equal(3, client.Fetches);
    }

    [Fact]
    public async Task TheMonitorReportsWhenItWillAcceptTheNextAttempt()
    {
        // This is what the loop waits on, rather than recomputing the cadence itself. The
        // monitor already merges what happened to the request into the signals; doing that twice
        // would be two copies of one rule, and they would diverge.
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(_ => Success(clock.Now)), clock);

        await monitor.RefreshAsync(new PollSignals());

        Assert.Equal(Start + PollPolicy.Ceiling, monitor.NextAttemptAt);
    }
}
