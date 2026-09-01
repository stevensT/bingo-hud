using BingoHud.Core.Credentials;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The monitor: one reading, one refresh in flight, and one backoff.
///
/// <para>
/// Everything upstream of this class is a pure function or a thin adapter. This is the only
/// place that remembers anything, which makes it the only place a stale number can be presented
/// as a current one — so most of what follows is about what happens to the previous reading when
/// the next attempt fails.
/// </para>
/// </summary>
public class QuotaMonitorTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.FromHours(-7));

    /// <summary>Signals that produce the slowest ordinary cadence, so backoff is predictable.</summary>
    private static readonly PollSignals Idle = new();

    private static QuotaSnapshot SnapshotAt(DateTimeOffset observedAt, double usedPercent = 12) =>
        new(
            [new QuotaWindow(WindowKind.Session, usedPercent, null, ServerSeverity.Normal)],
            observedAt,
            RawBody: "{}");

    private static FetchOutcome Success(DateTimeOffset observedAt, double usedPercent = 12) =>
        new FetchOutcome.Success(SnapshotAt(observedAt, usedPercent));

    private static QuotaMonitor Monitor(
        IUsageClient client,
        TestClock clock,
        ICredentialProvider? credentials = null) =>
        new(credentials ?? StubCredentialProvider.WithToken(), client, clock);

    // ---- Single flight (AC-27) ----

    [Fact]
    public async Task ConcurrentRefreshesCollapseToASingleFetch()
    {
        // AC-27. Every caller wants the same number, and the endpoint is rate-limited: asking
        // five times concurrently would spend five requests to learn one thing.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start)) { Gate = new TaskCompletionSource() };
        var monitor = Monitor(client, clock);

        var refreshes = Enumerable.Range(0, 5).Select(_ => monitor.RefreshAsync(Idle)).ToArray();
        client.Gate.SetResult();
        await Task.WhenAll(refreshes);

        Assert.Equal(1, client.Fetches);
    }

    [Fact]
    public async Task EveryConcurrentCallerGetsTheSameResult()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start)) { Gate = new TaskCompletionSource() };
        var monitor = Monitor(client, clock);

        var first = monitor.RefreshAsync(Idle);
        var second = monitor.RefreshAsync(Idle);
        client.Gate.SetResult();

        Assert.Same(await first, await second);
    }

    [Fact]
    public async Task TheCredentialIsReadOncePerFetchRatherThanOncePerCaller()
    {
        var clock = new TestClock(Start);
        var credentials = StubCredentialProvider.WithToken();
        var client = new StubUsageClient(Success(Start)) { Gate = new TaskCompletionSource() };
        var monitor = Monitor(client, clock, credentials);

        var refreshes = Enumerable.Range(0, 4).Select(_ => monitor.RefreshAsync(Idle)).ToArray();
        client.Gate.SetResult();
        await Task.WhenAll(refreshes);

        Assert.Equal(1, credentials.Reads);
    }

    [Fact]
    public async Task TheGuardIsReleasedSoALaterRefreshCanRun()
    {
        // A single-flight guard that is never cleared is indistinguishable from a working one
        // until the second poll never happens.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(call => Success(Start.AddMinutes(call)));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(2, client.Fetches);
    }

    // ---- First reading ----

    [Fact]
    public void BeforeAnyFetchThereIsNoReadingAtAll()
    {
        // Not a zero, not a placeholder. Nothing.
        var monitor = Monitor(new StubUsageClient(Success(Start)), new TestClock(Start));

        Assert.Null(monitor.Current.Last);
    }

    [Fact]
    public async Task AFirstSuccessBecomesTheCurrentReading()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start, 37)), clock);

        await monitor.RefreshAsync(Idle);

        Assert.Equal(37, monitor.Current.Last?.Windows.Single().UsedPercent);
    }

    [Fact]
    public async Task AFreshReadingHasNoAgeAndNoFailure()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        await monitor.RefreshAsync(Idle);

        Assert.Equal(TimeSpan.Zero, monitor.Current.Age);
        Assert.Equal(Freshness.Fresh, monitor.Current.Freshness);
        Assert.Null(monitor.Current.LastFailure);
    }

    [Fact]
    public async Task EveryStateCarriesTheReasonForTheNextPoll()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);

        await monitor.RefreshAsync(Idle);

        Assert.Equal(PollPolicy.Reasons.NothingIsHappening, monitor.Current.PollReason);
    }

    // ---- Age and staleness (AC-8) ----

    [Fact]
    public async Task TheAgeOfAReadingGrowsWithTheClock()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);
        await monitor.RefreshAsync(Idle);

        clock.Advance(TimeSpan.FromMinutes(9));

        Assert.Equal(TimeSpan.FromMinutes(9), monitor.Current.Age);
    }

    [Fact]
    public async Task AReadingWithinTheStalenessWindowIsStillFresh()
    {
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);
        await monitor.RefreshAsync(Idle);

        clock.Advance(QuotaMonitor.StaleAfter - TimeSpan.FromMinutes(1));

        Assert.Equal(Freshness.Fresh, monitor.Current.Freshness);
    }

    [Fact]
    public async Task AReadingOlderThanTheStalenessWindowGoesStale()
    {
        // The slowest ordinary cadence is thirty minutes, so a reading has to be allowed to be
        // that old without complaint. Past the window, a poll has been missed.
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start)), clock);
        await monitor.RefreshAsync(Idle);

        clock.Advance(QuotaMonitor.StaleAfter + TimeSpan.FromMinutes(1));

        Assert.Equal(Freshness.Stale, monitor.Current.Freshness);
    }

    [Fact]
    public async Task AStaleReadingIsStillTheReading()
    {
        // Marked, not discarded. It is the last true thing Bingo knows.
        var clock = new TestClock(Start);
        var monitor = Monitor(new StubUsageClient(Success(Start, 88)), clock);
        await monitor.RefreshAsync(Idle);

        clock.Advance(TimeSpan.FromHours(3));

        Assert.Equal(88, monitor.Current.Last?.Windows.Single().UsedPercent);
    }

    // ---- Failure after a success ----

    [Fact]
    public async Task AFailureKeepsTheLastReading()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start, 42),
            new FetchOutcome.Transient(null));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(42, monitor.Current.Last?.Windows.Single().UsedPercent);
    }

    [Fact]
    public async Task AFailureKeepsTheAgeMeasuredFromTheReadingRatherThanFromTheAttempt()
    {
        // The failed attempt is not a reading, so it cannot reset the clock on the last one.
        // Otherwise a run of failures would keep an hours-old number looking a minute old.
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(Success(Start), new FetchOutcome.Transient(null));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(TimeSpan.FromHours(1), monitor.Current.Age);
    }

    [Fact]
    public async Task AFailureIsRecordedAlongsideTheReadingItDidNotReplace()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(Success(Start), new FetchOutcome.Transient(null));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);

        Assert.IsType<FetchOutcome.Transient>(monitor.Current.LastFailure);
        Assert.NotNull(monitor.Current.Last);
    }

    [Fact]
    public async Task ARecoveryClearsTheFailureAndTheAge()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start),
            new FetchOutcome.Transient(null),
            Success(Start.AddHours(2), 55));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);

        Assert.Null(monitor.Current.LastFailure);
        Assert.Equal(TimeSpan.Zero, monitor.Current.Age);
        Assert.Equal(55, monitor.Current.Last?.Windows.Single().UsedPercent);
    }

    // ---- Unreadable (AC-9, AC-12) ----

    [Fact]
    public async Task AnUnreadableResponseWithNoPriorReadingShowsNothing()
    {
        // AC-12 at the point it matters most. There is no number to show and none is invented.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(new FetchOutcome.Unreadable("the payload moved"));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);

        Assert.Null(monitor.Current.Last);
        Assert.IsType<FetchOutcome.Unreadable>(monitor.Current.LastFailure);
    }

    [Fact]
    public async Task AnUnreadableResponseAfterASuccessKeepsTheOlderReading()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start, 42),
            new FetchOutcome.Unreadable("the payload moved"));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(42, monitor.Current.Last?.Windows.Single().UsedPercent);
    }

    // ---- Frozen (AC-13) ----

    [Fact]
    public async Task AnAuthenticationFailureFreezesTheReading()
    {
        // AC-13. Nothing will refresh this until the user signs in, so the number stays on
        // screen marked as frozen rather than pretending a retry is imminent.
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start),
            new FetchOutcome.AuthFailed(AuthFailureKind.Invalidated));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        // Past the backoff, or the second refresh is refused and nothing is recorded.
        clock.Advance(PollPolicy.Ceiling + TimeSpan.FromMinutes(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(Freshness.Frozen, monitor.Current.Freshness);
    }

    [Fact]
    public async Task AnUnsupportedEndpointFreezesTheReading()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(Success(Start), new FetchOutcome.Unsupported(404));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        // Past the backoff, or the second refresh is refused and nothing is recorded.
        clock.Advance(PollPolicy.Ceiling + TimeSpan.FromMinutes(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(Freshness.Frozen, monitor.Current.Freshness);
    }

    [Fact]
    public async Task ATransientFailureDoesNotFreezeTheReading()
    {
        // It is expected to pass. Freezing on it would mark a number dead that will refresh in
        // two minutes.
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(Success(Start), new FetchOutcome.Transient(null));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        // Past the backoff, or the second refresh is refused and nothing is recorded.
        clock.Advance(PollPolicy.Ceiling + TimeSpan.FromMinutes(1));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(Freshness.Fresh, monitor.Current.Freshness);
    }

    [Fact]
    public async Task AFrozenReadingKeepsItsAge()
    {
        var clock = new TestClock(Start);
        var client = StubUsageClient.Sequence(
            Success(Start),
            new FetchOutcome.AuthFailed(AuthFailureKind.Invalidated));
        var monitor = Monitor(client, clock);

        await monitor.RefreshAsync(Idle);
        clock.Advance(TimeSpan.FromHours(2));
        await monitor.RefreshAsync(Idle);

        Assert.Equal(TimeSpan.FromHours(2), monitor.Current.Age);
    }

    // ---- No credential (AC-10, AC-11) ----

    [Fact]
    public async Task NoCredentialFileAtAllReadsAsSignedOut()
    {
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start));
        var monitor = Monitor(client, clock, StubCredentialProvider.Without(CredentialAvailability.Absent));

        await monitor.RefreshAsync(Idle);

        var failure = Assert.IsType<FetchOutcome.AuthFailed>(monitor.Current.LastFailure);
        Assert.Equal(AuthFailureKind.SignedOut, failure.Kind);
    }

    [Fact]
    public async Task ACredentialFileThatCannotBeOpenedReadsAsPermissionDenied()
    {
        // AC-11. Telling this user to sign in would send them somewhere that cannot help.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start));
        var monitor = Monitor(client, clock, StubCredentialProvider.Without(CredentialAvailability.AccessDenied));

        await monitor.RefreshAsync(Idle);

        var failure = Assert.IsType<FetchOutcome.AuthFailed>(monitor.Current.LastFailure);
        Assert.Equal(AuthFailureKind.PermissionDenied, failure.Kind);
    }

    [Fact]
    public async Task ACredentialFileHeldByAnotherProcessIsTransientRatherThanASignInPrompt()
    {
        // Claude Code rewriting the token as Bingo reads it. Asking the user to sign in over a
        // race that resolves itself in milliseconds would be absurd.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start));
        var monitor = Monitor(client, clock, StubCredentialProvider.Without(CredentialAvailability.Busy));

        await monitor.RefreshAsync(Idle);

        Assert.IsType<FetchOutcome.Transient>(monitor.Current.LastFailure);
    }

    [Fact]
    public async Task AReadableFileWithNoTokenInItIsUnspecifiedRatherThanSignedOut()
    {
        // The file is there and readable, so "signed out" is a claim the evidence does not
        // support — something else is wrong with it.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start));
        var monitor = Monitor(client, clock, StubCredentialProvider.Without(CredentialAvailability.Readable));

        await monitor.RefreshAsync(Idle);

        var failure = Assert.IsType<FetchOutcome.AuthFailed>(monitor.Current.LastFailure);
        Assert.Equal(AuthFailureKind.Unspecified, failure.Kind);
    }

    [Fact]
    public async Task NoCredentialMeansNoRequestIsMade()
    {
        // Sending a blank bearer would produce a 401 and report the user as rejected when they
        // are simply not signed in.
        var clock = new TestClock(Start);
        var client = new StubUsageClient(Success(Start));
        var monitor = Monitor(client, clock, StubCredentialProvider.Without(CredentialAvailability.Absent));

        await monitor.RefreshAsync(Idle);

        Assert.Equal(0, client.Fetches);
    }
}
