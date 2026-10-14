using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Backing off, and refusing to be hurried (AC-26, AC-28).
///
/// <para>
/// Two rules that only look like one. A rate limit has to be honoured on the server's terms, and
/// a manual refresh has to be held to the same floor as an automatic poll — because the moment a
/// user can see a number they are unhappy with is exactly the moment they will press refresh
/// repeatedly, against the rate limit they are already worried about.
/// </para>
/// <para>
/// Refusing is a normal outcome here, not an error, so it says both halves of what a person
/// needs: why, and when asking again will work.
/// </para>
/// <para>
/// Most passed on arrival, because the monitor was already built to satisfy them, and were
/// verified by mutation instead: dropping the server's <c>Retry-After</c> from the schedule, or
/// removing the refusal check, fails several of them. The later ones on the idle cadence and the
/// floor's reason were written failing first, when the floor and the cadence were separated.
/// </para>
/// </summary>
public class QuotaMonitorBackoffTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.FromHours(-7));

    private static readonly PollSignals Idle = new();

    /// <summary>Someone with the detail panel open, which asks for the fastest cadence there is.</summary>
    private static readonly PollSignals PanelOpen = new(SinceUserOpenedPanel: TimeSpan.Zero);

    private static FetchOutcome Success(DateTimeOffset observedAt) =>
        new FetchOutcome.Success(new QuotaSnapshot(
            [new QuotaWindow(WindowKind.Session, 12, null, ServerSeverity.Normal)],
            observedAt,
            RawBody: "{}"));

    private static QuotaMonitor Monitor(IUsageClient client, TestClock clock) =>
        new(StubCredentialProvider.WithToken(), client, clock);

    // ---- Rate limits (AC-26) ----

    [Fact]
    public async Task ARateLimitedResponseSchedulesTheServersOwnWait()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start),
            new FetchOutcome.Transient(TimeSpan.FromMinutes(7)));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(PollPolicy.Ceiling);
        await monitor.RefreshAsync(Idle);

        var refused = Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
        Assert.Equal(clock.Now.AddMinutes(7), refused.NextAttemptAt);
    }

    [Fact]
    public async Task AServersWaitIsNotShortenedByAnOpenPanel()
    {
        // The panel being open is the strongest local reason to poll fast, and it still does not
        // outrank the service saying wait.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Transient(TimeSpan.FromMinutes(20)));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(PanelOpen);
        clock.Advance(TimeSpan.FromMinutes(19));

        Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
    }

    [Fact]
    public async Task OnceTheServersWaitHasElapsedTheNextFetchRuns()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(call =>
            call == 0 ? new FetchOutcome.Transient(TimeSpan.FromMinutes(7)) : Success(clock.Now));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromMinutes(7));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(2, client.Fetches);
    }

    [Fact]
    public async Task ARateLimitProducesNoExtraRequests()
    {
        // AC-26. Nothing reaches for another endpoint to make up for the refusal — the fallback
        // that would is a non-goal precisely because it would spend real quota to read a number,
        // and add load to the limit that just said no.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Transient(TimeSpan.FromMinutes(3)));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);

        Assert.Equal(1, client.Fetches);
    }

    [Fact]
    public async Task ARateLimitWithNoRetryAfterStillBacksOff()
    {
        // The server did not say how long, which is not permission to come straight back.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Transient(null));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(PanelOpen);
        clock.Advance(PollPolicy.Floor);

        Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
    }

    // ---- Manual refresh (AC-28) ----

    [Fact]
    public async Task AManualRefreshRightAfterAPollIsRefused()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        await monitor.RefreshAsync(Idle);

        Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
    }

    [Fact]
    public async Task ARefusalSaysWhenTheNextAttemptIsPossible()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        await monitor.RefreshAsync(PanelOpen);

        var refused = Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
        Assert.Equal(Start + PollPolicy.Floor, refused.NextAttemptAt);
    }

    [Fact]
    public async Task ARefusalSaysWhy()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        await monitor.RefreshAsync(PanelOpen);

        var refused = Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
        Assert.False(string.IsNullOrWhiteSpace(refused.Reason));
    }

    [Fact]
    public async Task ARefusedRefreshDoesNotReachTheEndpoint()
    {
        // The whole point. A refusal that still made the request would be a lie told politely.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(PanelOpen);
        await monitor.RefreshAsync(PanelOpen);
        await monitor.RefreshAsync(PanelOpen);

        Assert.Equal(1, client.Fetches);
    }

    [Fact]
    public async Task AManualRefreshCannotBeatTheFloorHoweverOftenItIsPressed()
    {
        // AC-28 in the form it will actually be met: a user pressing refresh repeatedly at the
        // moment they are worried about their quota.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(call => Success(Start.AddSeconds(call)));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(PanelOpen);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            await monitor.RefreshAsync(PanelOpen);
        }

        // 100 seconds of pressing, against a two-minute floor.
        Assert.Equal(1, client.Fetches);
    }

    [Fact]
    public async Task AManualRefreshIsAllowedOnceTheFloorHasElapsed()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(call => Success(Start.AddMinutes(call)));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(PanelOpen);
        clock.Advance(PollPolicy.Floor);

        Assert.IsType<RefreshResult.Performed>(await monitor.RefreshAsync(PanelOpen));
    }

    [Fact]
    public async Task AnIdleCadenceDoesNotHoldAManualRefreshPastTheFloor()
    {
        // The idle cadence is the slowest Bingo chooses, and it is a preference, not a
        // restraint. Opening the panel five minutes after a quiet poll and pressing refresh was
        // once refused for the remaining twenty-five, which left the manual refresh unable to
        // cover the one case it exists for.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(call => Success(clock.Now));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.IsType<RefreshResult.Performed>(await monitor.RefreshAsync(PanelOpen));
        Assert.Equal(2, client.Fetches);
    }

    [Fact]
    public async Task ARefusalInsideTheFloorSaysBingoJustChecked()
    {
        // The cadence's own reason would be false here: an idle cadence did not refuse this,
        // the floor did.
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromMinutes(1));

        var refused = Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
        Assert.Equal(PollPolicy.Reasons.JustChecked, refused.Reason);
        Assert.Equal(Start + PollPolicy.Floor, refused.NextAttemptAt);
    }

    [Fact]
    public async Task AFailedAttemptStillHoldsAManualRefreshUntilItsBackoffEnds()
    {
        // Only the cadence was loosened. A backoff after a failure is a restraint, and an open
        // panel does not get past it.
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(new FetchOutcome.Unreadable("broken")), clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromMinutes(5));

        var refused = Assert.IsType<RefreshResult.Refused>(await monitor.RefreshAsync(PanelOpen));
        Assert.Equal(PollPolicy.Reasons.LastAttemptFailed, refused.Reason);
        Assert.Equal(Start + PollPolicy.Ceiling, refused.NextAttemptAt);
    }

    [Fact]
    public async Task AnAttemptAtTheCadencesTimeIsNeverRefusedEvenOnAMovingClock()
    {
        // The loop waits until NextAttemptAt and then asks. If the floor were measured from a
        // later reading of the clock than the cadence was, the floor could land a tick past the
        // cadence when both are two minutes, and the loop would be refused with nothing left to
        // wait for and go round again at once. A clock that moves on every read makes that
        // ordering visible.
        var clock = new TickingClock(Start);
        var monitor = new QuotaMonitor(
            StubCredentialProvider.WithToken(), new StubUsageClient(_ => Success(clock.Now)), clock);

        await monitor.RefreshAsync(PanelOpen);
        clock.Set(monitor.NextAttemptAt!.Value);

        Assert.IsType<RefreshResult.Performed>(await monitor.RefreshAsync(PanelOpen));
    }

    /// <summary>A clock that moves forward one tick every time it is read.</summary>
    private sealed class TickingClock(DateTimeOffset start) : Time.IClock
    {
        private DateTimeOffset _now = start;

        public DateTimeOffset Now => _now = _now.AddTicks(1);

        /// <summary>The next read returns exactly <paramref name="to"/>.</summary>
        public void Set(DateTimeOffset to) => _now = to.AddTicks(-1);

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task TheFirstRefreshOfAllIsNeverRefused()
    {
        // Nothing has been asked of the endpoint yet, so there is nothing to back off from.
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        Assert.IsType<RefreshResult.Performed>(await monitor.RefreshAsync(Idle));
    }
}
