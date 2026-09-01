using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The single severity the HUD reports, derived from a whole reading.
///
/// <para>
/// Four rules, and each exists because the obvious alternative is wrong. Thresholds are stated
/// as remaining while the stored values are consumed, so the conversion happens in exactly one
/// place. The worst window drives the result, because a session window at 5% is the user's
/// problem regardless of how healthy the weekly one looks. A server rejection is its own state
/// rather than a louder critical, because "the service is refusing work" and "Bingo thinks this
/// number is low" call for different reactions. And a frozen reading contributes nothing at all,
/// because a number that can no longer be refreshed must not own the headline.
/// </para>
/// </summary>
public class SeverityPolicyTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 31, 10, 9, 23, TimeSpan.FromHours(-7));

    private static QuotaSnapshot Snapshot(params QuotaWindow[] windows) =>
        new(windows, ObservedAt, RawBody: "{}");

    private static QuotaWindow Window(
        double usedPercent,
        WindowKind kind = WindowKind.Session,
        ServerSeverity severity = ServerSeverity.Normal) =>
        new(kind, usedPercent, ResetsAt: null, severity);

    private static Severity Evaluate(QuotaSnapshot snapshot, Freshness freshness = Freshness.Fresh) =>
        SeverityPolicy.Evaluate(snapshot, Thresholds.Default, freshness);

    [Theory]
    [InlineData(0, Severity.Normal)]
    [InlineData(50, Severity.Normal)]
    [InlineData(74, Severity.Normal)]
    [InlineData(75, Severity.Warning)]
    [InlineData(80, Severity.Warning)]
    [InlineData(89, Severity.Warning)]
    [InlineData(90, Severity.Critical)]
    [InlineData(99, Severity.Critical)]
    [InlineData(100, Severity.Critical)]
    public void TheThreeStatesFallWhereTheThresholdsSay(double usedPercent, Severity expected)
    {
        // 75% consumed is 25% remaining, and 90% consumed is 10% remaining. The thresholds are
        // written as remaining because that is how someone thinks about running out.
        Assert.Equal(expected, Evaluate(Snapshot(Window(usedPercent))));
    }

    [Fact]
    public void TheWorstWindowDrivesTheResult()
    {
        var snapshot = Snapshot(
            Window(12, WindowKind.Session),
            Window(95, WindowKind.WeeklyAll));

        Assert.Equal(Severity.Critical, Evaluate(snapshot));
    }

    [Fact]
    public void TheWorstWindowDrivesTheResultWhicheverOrderTheyArrive()
    {
        var snapshot = Snapshot(
            Window(95, WindowKind.Session),
            Window(12, WindowKind.WeeklyAll));

        Assert.Equal(Severity.Critical, Evaluate(snapshot));
    }

    [Fact]
    public void AServerRejectionIsItsOwnStateRatherThanACritical()
    {
        // AC-6. The percentage here is entirely healthy, and the service is still refusing.
        var snapshot = Snapshot(Window(12, severity: ServerSeverity.Rejected));

        Assert.Equal(Severity.RateLimited, Evaluate(snapshot));
    }

    [Fact]
    public void AServerRejectionOutranksALocalCritical()
    {
        var snapshot = Snapshot(
            Window(99, WindowKind.Session),
            Window(12, WindowKind.WeeklyAll, ServerSeverity.Rejected));

        Assert.Equal(Severity.RateLimited, Evaluate(snapshot));
    }

    [Theory]
    [InlineData(ServerSeverity.Warning, Severity.Warning)]
    [InlineData(ServerSeverity.Critical, Severity.Critical)]
    public void AServerEscalationIsHonouredEvenWhenThePercentageLooksFine(
        ServerSeverity reported,
        Severity expected)
    {
        // Neither spelling has been observed on a live response. If one arrives while the
        // percentage still looks healthy, the server knows something Bingo does not, and
        // ignoring it in favour of a local threshold would be the wrong way round.
        var snapshot = Snapshot(Window(3, severity: reported));

        Assert.Equal(expected, Evaluate(snapshot));
    }

    [Fact]
    public void AServerReportingNormalDoesNotSuppressALocalThreshold()
    {
        // The direction that matters: severity is only ever raised by the server, never
        // lowered. Every live capture so far says "normal" for every window.
        var snapshot = Snapshot(Window(95, severity: ServerSeverity.Normal));

        Assert.Equal(Severity.Critical, Evaluate(snapshot));
    }

    [Fact]
    public void AnUnknownServerSeverityLeavesTheLocalThresholdInCharge()
    {
        var snapshot = Snapshot(Window(95, severity: ServerSeverity.Unknown));

        Assert.Equal(Severity.Critical, Evaluate(snapshot));
    }

    [Fact]
    public void AFrozenReadingDoesNotDetermineSeverity()
    {
        // AC-13. The numbers may be hours old; acting on them is exactly what the frozen state
        // exists to prevent.
        var snapshot = Snapshot(Window(99, WindowKind.Session));

        Assert.Equal(Severity.Normal, Evaluate(snapshot, Freshness.Frozen));
    }

    [Fact]
    public void AFrozenReadingDoesNotSurfaceAServerRejectionEither()
    {
        var snapshot = Snapshot(Window(12, severity: ServerSeverity.Rejected));

        Assert.Equal(Severity.Normal, Evaluate(snapshot, Freshness.Frozen));
    }

    [Fact]
    public void AStaleReadingStillDeterminesSeverity()
    {
        // Only Frozen is excluded. A stale reading is old but still refreshable, and its age is
        // shown alongside it — suppressing its severity would hide a real problem.
        var snapshot = Snapshot(Window(99));

        Assert.Equal(Severity.Critical, Evaluate(snapshot, Freshness.Stale));
    }

    [Fact]
    public void ThresholdsAreConfigurable()
    {
        // 6.2 makes these a user setting. The policy must read them rather than assume them.
        var strict = new Thresholds(WarningAtRemaining: 60, CriticalAtRemaining: 50);
        var snapshot = Snapshot(Window(45));

        Assert.Equal(Severity.Warning, SeverityPolicy.Evaluate(snapshot, strict, Freshness.Fresh));
    }

    [Fact]
    public void TheDefaultThresholdsAreTheOnesTheSpecStates()
    {
        Assert.Equal(25, Thresholds.Default.WarningAtRemaining);
        Assert.Equal(10, Thresholds.Default.CriticalAtRemaining);
    }
}
