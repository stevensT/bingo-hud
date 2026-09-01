using BingoHud.Core.Alerts;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Which alerts a reading is due, and which it has already had.
///
/// <para>
/// The engine decides; it does not notify. Raising the toast is the shell's job, so what is
/// tested here is the decision — which is the part that can be wrong in a way nobody notices
/// until an alert fires twelve times or not at all.
/// </para>
/// <para>
/// Two rules carry the behaviour. An alert is due when a window is at or beyond a threshold and
/// that key has not fired, rather than when a reading crosses the line between two polls —
/// otherwise launching Bingo when already at 5% remaining would say nothing at all, which is
/// precisely the moment it exists for. And when a reading arrives already past both lines, only
/// the more severe alert is due, but both are recorded — the warning has been overtaken by
/// events and must not arrive afterwards as though it were news.
/// </para>
/// </summary>
public class AlertEngineTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 31, 10, 9, 23, TimeSpan.FromHours(-7));

    private static readonly DateTimeOffset Reset =
        new(2026, 8, 31, 16, 0, 0, TimeSpan.FromHours(-7));

    /// <summary>An in-memory stand-in for the persistent store built in 5.3.</summary>
    private sealed class RecordingStore : IAlertStateStore
    {
        private readonly HashSet<AlertKey> _fired = [];

        public bool HasFired(AlertKey key) => _fired.Contains(key);

        public void MarkFired(AlertKey key) => _fired.Add(key);
    }

    private static QuotaWindow Window(
        double usedPercent,
        WindowKind kind = WindowKind.Session,
        DateTimeOffset? resetsAt = null) =>
        new(kind, usedPercent, resetsAt ?? Reset, ServerSeverity.Normal);

    private static QuotaSnapshot Snapshot(params QuotaWindow[] windows) =>
        new(windows, ObservedAt, RawBody: "{}");

    private static AlertEngine Engine(IAlertStateStore? store = null) =>
        new(store ?? new RecordingStore());

    private static IReadOnlyList<Alert> Take(AlertEngine engine, params QuotaWindow[] windows) =>
        engine.TakeNewAlerts(Snapshot(windows), Thresholds.Default);

    [Fact]
    public void AWindowInsideItsThresholdsIsDueNothing()
    {
        Assert.Empty(Take(Engine(), Window(usedPercent: 50)));
    }

    [Fact]
    public void ReachingTheWarningLineIsDueOneWarning()
    {
        // 75% consumed is 25% remaining — the warning line exactly, and at or beyond counts.
        var alert = Assert.Single(Take(Engine(), Window(usedPercent: 75)));

        Assert.Equal(Severity.Warning, alert.Severity);
        Assert.Equal(new AlertKey(WindowKind.Session, ThresholdPercent: 25, Reset), alert.Key);
        Assert.Equal(75, alert.UsedPercent);
    }

    [Fact]
    public void TheSameThresholdIsNotDueASecondTimeInTheSameWindowOccurrence()
    {
        var engine = Engine();

        Assert.Single(Take(engine, Window(usedPercent: 75)));
        Assert.Empty(Take(engine, Window(usedPercent: 80)));
    }

    [Fact]
    public void TheAlertIsDueAgainOnceTheWindowHasReset()
    {
        var engine = Engine();
        Assert.Single(Take(engine, Window(usedPercent: 80)));

        var nextOccurrence = Window(usedPercent: 80, resetsAt: Reset.AddHours(5));

        var alert = Assert.Single(Take(engine, nextOccurrence));
        Assert.Equal(Severity.Warning, alert.Severity);
    }

    [Fact]
    public void WarningThenCriticalAreTwoAlertsWithinOneOccurrence()
    {
        var engine = Engine();

        Assert.Equal(Severity.Warning, Assert.Single(Take(engine, Window(usedPercent: 80))).Severity);
        Assert.Equal(Severity.Critical, Assert.Single(Take(engine, Window(usedPercent: 95))).Severity);
    }

    [Fact]
    public void AReadingAlreadyPastBothLinesIsDueOnlyTheCriticalOne()
    {
        var alert = Assert.Single(Take(Engine(), Window(usedPercent: 95)));

        Assert.Equal(Severity.Critical, alert.Severity);
        Assert.Equal(10, alert.Key.ThresholdPercent);
    }

    [Fact]
    public void TheWarningSkippedPastIsRecordedSoItCannotArriveLate()
    {
        var engine = Engine();
        Take(engine, Window(usedPercent: 95));

        // Consumption cannot fall within an occurrence, so this is the same reading again. If the
        // overtaken warning had not been recorded it would surface here, after the critical.
        Assert.Empty(Take(engine, Window(usedPercent: 96)));
    }

    [Fact]
    public void TheFirstReadingOfASessionAlertsEvenThoughNoCrossingWasObserved()
    {
        // Bingo launched at 8% remaining. There is no previous reading to have crossed from, and
        // this is exactly when the user needs telling.
        var alert = Assert.Single(Take(Engine(), Window(usedPercent: 92)));

        Assert.Equal(Severity.Critical, alert.Severity);
    }

    [Fact]
    public void TheTwoWindowsAlertIndependentlyInOneReading()
    {
        var alerts = Take(
            Engine(),
            Window(usedPercent: 80, kind: WindowKind.Session),
            Window(usedPercent: 95, kind: WindowKind.WeeklyAll));

        Assert.Equal(2, alerts.Count);
        Assert.Equal(WindowKind.Session, alerts[0].Key.Kind);
        Assert.Equal(Severity.Warning, alerts[0].Severity);
        Assert.Equal(WindowKind.WeeklyAll, alerts[1].Key.Kind);
        Assert.Equal(Severity.Critical, alerts[1].Severity);
    }

    [Fact]
    public void AWindowWithNoResetTimeNeverAlerts()
    {
        // No occurrence means no once-per, so it would fire on every poll forever. It gets no key
        // in 5.1, and here that shows up as silence rather than as a crash.
        var window = new QuotaWindow(
            WindowKind.Session, UsedPercent: 99, ResetsAt: null, ServerSeverity.Normal);

        Assert.Empty(Take(Engine(), window));
    }

    [Fact]
    public void OnlyTheAlertsActuallyDueAreRecorded()
    {
        // A quiet reading must not mark anything fired, or the real alert would be swallowed.
        var store = new RecordingStore();
        Take(Engine(store), Window(usedPercent: 50));

        Assert.False(store.HasFired(new AlertKey(WindowKind.Session, 25, Reset)));
        Assert.False(store.HasFired(new AlertKey(WindowKind.Session, 10, Reset)));
    }
}
