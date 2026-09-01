using System.Diagnostics;
using BingoHud.Core.Time;

namespace BingoHud.Core.Tests;

/// <summary>
/// Waiting, as something Core asks the clock for rather than does itself.
///
/// <para>
/// A loop that calls <c>Task.Delay</c> directly cannot be tested without actually waiting, and a
/// cadence measured in tens of minutes is not something a test suite can sit through. Putting
/// the wait behind the same seam as "now" keeps one source of time in Core instead of two, and
/// lets <see cref="TestClock"/> collapse half an hour into nothing.
/// </para>
/// </summary>
public class ClockDelayTests
{
    [Fact]
    public async Task TheTestClockCompletesADelayImmediatelyAndMovesItselfForward()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));
        var started = Stopwatch.StartNew();

        await clock.DelayAsync(TimeSpan.FromMinutes(30), CancellationToken.None);

        started.Stop();
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 9, 30, 0, TimeSpan.Zero), clock.Now);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1), "the test clock must not really wait");
    }

    [Fact]
    public async Task TheTestClockRecordsWhatItWasAskedToWaitFor()
    {
        // A loop's cadence is only observable as the spans it asks for, so the test clock has to
        // keep them.
        var clock = new TestClock(DateTimeOffset.UnixEpoch);

        await clock.DelayAsync(TimeSpan.FromMinutes(2), CancellationToken.None);
        await clock.DelayAsync(TimeSpan.FromMinutes(30), CancellationToken.None);

        Assert.Equal(
            [TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30)],
            clock.RequestedDelays);
    }

    [Fact]
    public async Task ADelayThatIsAlreadyCancelledDoesNotProceed()
    {
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => clock.DelayAsync(TimeSpan.FromMinutes(5), cancellation.Token));
    }

    [Fact]
    public async Task TheSystemClockActuallyWaits()
    {
        var clock = new SystemClock();
        var started = Stopwatch.StartNew();

        await clock.DelayAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        started.Stop();
        // Generous: the assertion is that it waited at all, not that the timer is precise.
        Assert.True(
            started.Elapsed >= TimeSpan.FromMilliseconds(25),
            $"expected a real wait, got {started.Elapsed}");
    }

    [Fact]
    public async Task TheSystemClockStopsWaitingWhenCancelled()
    {
        var clock = new SystemClock();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => clock.DelayAsync(TimeSpan.FromMinutes(30), cancellation.Token));
    }
}
