using BingoHud.Core.Alerts;
using BingoHud.Core.Time;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Silencing the window you already know about.
///
/// <para>
/// Muting is not a new kind of state. "Do not tell me about this window again" and "you have
/// already told me about this window" are the same instruction to the same record, so a mute is
/// simply every threshold for that occurrence marked as fired. That is why muting survives a
/// restart and lifts on reset without either behaviour being written twice — both already belong
/// to <see cref="AlertKey"/>.
/// </para>
/// <para>
/// It follows that a mute is per window and lasts until that window resets. There is no
/// indefinite mute, because a quota tool that can be silenced permanently is a quota tool that
/// will be silent on the day it matters.
/// </para>
/// </summary>
public class AlertMuteTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 31, 10, 9, 23, TimeSpan.FromHours(-7));

    private static readonly DateTimeOffset Reset =
        new(2026, 8, 31, 16, 0, 0, TimeSpan.FromHours(-7));

    private static QuotaWindow Window(
        double usedPercent,
        WindowKind kind = WindowKind.Session,
        DateTimeOffset? resetsAt = null) =>
        new(kind, usedPercent, resetsAt ?? Reset, ServerSeverity.Normal);

    private static IReadOnlyList<Alert> Take(AlertEngine engine, params QuotaWindow[] windows) =>
        engine.TakeNewAlerts(new QuotaSnapshot(windows, ObservedAt, "{}"), Thresholds.Default);

    [Fact]
    public void AMutedWindowRaisesNothingItWouldOtherwiseHaveRaised()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());

        engine.Mute(Window(usedPercent: 50), Thresholds.Default);

        Assert.Empty(Take(engine, Window(usedPercent: 95)));
    }

    [Fact]
    public void MutingSilencesTheCriticalLineToNotJustTheOneAlreadyCrossed()
    {
        // Muted at the warning line, the user must not then be shouted at by the critical one.
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        Assert.Single(Take(engine, Window(usedPercent: 80)));

        engine.Mute(Window(usedPercent: 80), Thresholds.Default);

        Assert.Empty(Take(engine, Window(usedPercent: 99)));
    }

    [Fact]
    public void MutingOneWindowLeavesTheOtherAlone()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());

        engine.Mute(Window(usedPercent: 95, kind: WindowKind.Session), Thresholds.Default);

        var alert = Assert.Single(Take(
            engine,
            Window(usedPercent: 95, kind: WindowKind.Session),
            Window(usedPercent: 95, kind: WindowKind.WeeklyAll)));

        Assert.Equal(WindowKind.WeeklyAll, alert.Key.Kind);
    }

    [Fact]
    public void TheMuteLiftsWhenTheWindowResets()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        engine.Mute(Window(usedPercent: 95), Thresholds.Default);

        var nextOccurrence = Window(usedPercent: 95, resetsAt: Reset.AddHours(5));

        Assert.Single(Take(engine, nextOccurrence));
    }

    [Fact]
    public void AMuteSurvivesARestartWithinTheSameWindow()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");
        var clock = new TestClock(ObservedAt);

        new AlertEngine(new AlertStateStore(path, clock)).Mute(Window(95), Thresholds.Default);

        var afterRestart = new AlertEngine(new AlertStateStore(path, clock));

        Assert.Empty(Take(afterRestart, Window(usedPercent: 95)));
    }

    [Fact]
    public void MutingAWindowWithNoResetTimeIsHarmless()
    {
        // It has no occurrence to mute, and 5.1 already means it never alerts at all.
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        var window = new QuotaWindow(
            WindowKind.Session, UsedPercent: 99, ResetsAt: null, ServerSeverity.Normal);

        engine.Mute(window, Thresholds.Default);

        Assert.Empty(Take(engine, window));
    }
}
