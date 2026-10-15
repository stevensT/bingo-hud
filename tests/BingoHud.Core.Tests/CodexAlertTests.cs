using System.Globalization;
using BingoHud.Core.Alerts;
using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Codex alerts, and notifications that say whose they are (codex-quota AC-10, AC-3).
///
/// <para>
/// Codex windows alert on the same thresholds as Claude's, with one difference that comes from
/// the source. A Codex reading can be hours old, so the window it describes may already have
/// reset. That figure belongs to an ended occurrence, and announcing it would wake the user about
/// a limit that has already lifted.
/// </para>
/// </summary>
public class CodexAlertTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static CodexState Reading(double session, double sessionResetInMinutes = 53, double weekly = 20) => new(
        CodexPresence.Found,
        new QuotaSnapshot(
            [
                new QuotaWindow(WindowKind.Session, session, Now.AddMinutes(sessionResetInMinutes), ServerSeverity.Unknown),
                new QuotaWindow(WindowKind.WeeklyAll, weekly, Now.AddDays(2), ServerSeverity.Unknown),
            ],
            Now.AddHours(-1),
            "{}"),
        Problem: null,
        TimeSpan.FromHours(1),
        ReachedType: null,
        []);

    private static AlertEngine Engine() => new(new InMemoryAlertStateStore());

    // --- Which Codex alerts are due

    [Fact]
    public void ACodexWindowPastAThresholdIsDueACodexAlert()
    {
        var alert = Assert.Single(CodexAlerts.TakeNew(Engine(), Reading(session: 95), Thresholds.Default, Now));

        Assert.Equal(Product.Codex, alert.Key.Product);
        Assert.Equal(WindowKind.Session, alert.Key.Kind);
        Assert.Equal(Severity.Critical, alert.Severity);
    }

    [Fact]
    public void AWindowWhoseResetHasPassedRaisesNothing()
    {
        // Last used yesterday evening at 95%; that window reset overnight.
        Assert.Empty(CodexAlerts.TakeNew(Engine(), Reading(session: 95, sessionResetInMinutes: -60), Thresholds.Default, Now));
    }

    [Fact]
    public void AResetAtExactlyNowRaisesNothing()
    {
        Assert.Empty(CodexAlerts.TakeNew(Engine(), Reading(session: 95, sessionResetInMinutes: 0), Thresholds.Default, Now));
    }

    [Fact]
    public void TheOtherWindowStillAlerts()
    {
        var due = CodexAlerts.TakeNew(Engine(), Reading(session: 95, sessionResetInMinutes: -60, weekly: 92), Thresholds.Default, Now);

        Assert.Equal(WindowKind.WeeklyAll, Assert.Single(due).Key.Kind);
    }

    [Fact]
    public void NoReadingRaisesNothing()
    {
        var none = new CodexState(CodexPresence.Found, null, null, TimeSpan.Zero, null, []);

        Assert.Empty(CodexAlerts.TakeNew(Engine(), none, Thresholds.Default, Now));
    }

    [Fact]
    public void ACodexAlertIsDueOncePerOccurrence()
    {
        var engine = Engine();

        Assert.Single(CodexAlerts.TakeNew(engine, Reading(session: 95), Thresholds.Default, Now));
        Assert.Empty(CodexAlerts.TakeNew(engine, Reading(session: 96), Thresholds.Default, Now));
    }

    // --- Notifications name their product

    private static Alert Alert(Product product, WindowKind kind = WindowKind.Session, Severity severity = Severity.Warning) =>
        new(new AlertKey(kind, 25, Now.AddMinutes(53), product), severity, UsedPercent: 78);

    [Fact]
    public void AClaudeNotificationSaysClaude()
    {
        var message = AlertMessage.Describe(Alert(Product.Claude), DisplayDirection.Consumed, Now, TwelveHour);

        Assert.Equal("Claude 5h window running low", message.Title);
    }

    [Fact]
    public void ACodexNotificationSaysCodex()
    {
        var message = AlertMessage.Describe(
            Alert(Product.Codex, WindowKind.WeeklyAll, Severity.Critical), DisplayDirection.Consumed, Now, TwelveHour);

        Assert.Equal("Codex Week window nearly used up", message.Title);
    }

    [Fact]
    public void ABatchNamesEachWindowsProduct()
    {
        var message = AlertMessage.Describe(
            [Alert(Product.Claude), Alert(Product.Codex)], DisplayDirection.Consumed, Now, TwelveHour);

        Assert.NotNull(message);
        Assert.Equal("2 windows running low", message.Title);
        Assert.Equal(
            "Claude 5h: 78% used, resets in 53 min." + Environment.NewLine
            + "Codex 5h: 78% used, resets in 53 min.",
            message.Body);
    }
}
