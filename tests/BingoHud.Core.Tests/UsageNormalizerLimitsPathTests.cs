using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The primary parse path, against the real 200 captured on 2026-08-30.
///
/// The live payload carries a self-describing <c>limits[]</c> array alongside the older flat
/// window keys, and on capture day the two agreed exactly. <c>limits[]</c> is preferred because
/// it needs no alias map and carries the server's own severity, so it is what these tests pin.
/// </summary>
public class UsageNormalizerLimitsPathTests
{
    /// <summary>
    /// An arbitrary but fixed observation instant. Nothing about parsing depends on when it
    /// happened; the value only has to come back out unchanged.
    /// </summary>
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    /// <summary>
    /// Parses the baseline fixture and asserts the outcome was a success.
    ///
    /// Asserting the outcome type here rather than in every test means a regression that turns
    /// the baseline unreadable reports itself as exactly that, instead of as a cast failure.
    /// </summary>
    private static QuotaSnapshot ParseBaseline()
    {
        var outcome = UsageNormalizer.Normalize(Fixtures.Read(Fixtures.Baseline), ObservedAt);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        return success.Snapshot;
    }

    [Fact]
    public void BaselineYieldsExactlyTwoWindows()
    {
        var snapshot = ParseBaseline();

        Assert.Equal(2, snapshot.Windows.Count);
    }

    [Fact]
    public void BaselineYieldsTheSessionAndWeeklyAllWindows()
    {
        var snapshot = ParseBaseline();

        Assert.Equal(
            new[] { WindowKind.Session, WindowKind.WeeklyAll },
            snapshot.Windows.Select(w => w.Kind));
    }

    [Fact]
    public void SessionWindowCarriesTheUtilizationTheServerReported()
    {
        var session = ParseBaseline().Windows.Single(w => w.Kind == WindowKind.Session);

        Assert.Equal(12, session.UsedPercent);
    }

    [Fact]
    public void WeeklyAllWindowCarriesTheUtilizationTheServerReported()
    {
        var weekly = ParseBaseline().Windows.Single(w => w.Kind == WindowKind.WeeklyAll);

        Assert.Equal(37, weekly.UsedPercent);
    }

    [Fact]
    public void SessionResetInstantKeepsTheMicrosecondsTheServerSent()
    {
        // "2026-08-30T21:30:00.972286+00:00" — microsecond precision, and an explicit +00:00
        // offset rather than a Z. A parser that truncated to whole seconds would still look
        // right on screen, so the assertion is built tick by tick instead of round-tripped
        // through the same parse it is meant to be checking.
        var expected = new DateTimeOffset(2026, 8, 30, 21, 30, 0, TimeSpan.Zero)
            .AddTicks(9_722_860);

        var session = ParseBaseline().Windows.Single(w => w.Kind == WindowKind.Session);

        Assert.Equal(expected, session.ResetsAt);
    }

    [Fact]
    public void WeeklyAllResetInstantKeepsTheMicrosecondsTheServerSent()
    {
        // "2026-09-05T01:00:00.972302+00:00"
        var expected = new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero)
            .AddTicks(9_723_020);

        var weekly = ParseBaseline().Windows.Single(w => w.Kind == WindowKind.WeeklyAll);

        Assert.Equal(expected, weekly.ResetsAt);
    }

    [Fact]
    public void SnapshotCarriesTheInstantItWasObserved()
    {
        // Principle 6: every figure on screen carries its age, which is measured from here.
        var snapshot = ParseBaseline();

        Assert.Equal(ObservedAt, snapshot.ObservedAt);
    }

    [Fact]
    public void SnapshotRetainsTheBodyItWasParsedFrom()
    {
        // When the payload drifts, the first diagnostic question is what the server actually
        // sent. Holding the body makes that answerable from the running app.
        var snapshot = ParseBaseline();

        Assert.Equal(Fixtures.Read(Fixtures.Baseline), snapshot.RawBody);
    }
}
