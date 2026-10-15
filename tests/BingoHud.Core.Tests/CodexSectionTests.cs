using System.Globalization;
using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// What the Codex section says about its own reading (codex-quota AC-2, AC-3, AC-3a, AC-7).
///
/// <para>
/// A Codex reading is as old as the last Codex turn, which can be hours, so unlike Claude's it
/// always carries its age. An old figure is still a true one: Codex has simply not been used
/// since, and it keeps its colours. What drops them is Bingo failing to read the logs, because
/// then it cannot tell whether Codex has been used since. And a window whose reset has passed
/// shows no figure at all, because the one Bingo holds belongs to an occurrence that has ended,
/// and nothing has been said about the new one.
/// </para>
/// </summary>
public class CodexSectionTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static readonly ReadingState NoClaude = new(null, Freshness.Fresh, null, TimeSpan.Zero, "test");

    private static QuotaSnapshot Snapshot(
        double session = 30, double weekly = 20,
        double sessionResetInMinutes = 53, double weeklyResetInMinutes = 3000) => new(
        [
            new QuotaWindow(WindowKind.Session, session, Now.AddMinutes(sessionResetInMinutes), ServerSeverity.Unknown),
            new QuotaWindow(WindowKind.WeeklyAll, weekly, Now.AddMinutes(weeklyResetInMinutes), ServerSeverity.Unknown),
        ],
        Now,
        "{}");

    private static CodexState Found(QuotaSnapshot? snapshot, double ageInMinutes = 12, CodexProblem? problem = null) =>
        new(CodexPresence.Found, snapshot, problem, TimeSpan.FromMinutes(ageInMinutes), ReachedType: null, []);

    private static HudContent Section(CodexState codex, UserSettings? settings = null)
    {
        var hud = Readout.Compose(
            NoClaude,
            codex,
            (settings ?? UserSettings.Default) with { Products = new ProductSwitches(Claude: false, Codex: true) },
            Now,
            TwelveHour);

        return Assert.Single(hud.Sections).Content;
    }

    private static HudContent.Reading Reading(CodexState codex, UserSettings? settings = null) =>
        Assert.IsType<HudContent.Reading>(Section(codex, settings));

    private static readonly CodexProblem Locked = new CodexProblem.LogsUnreadable("A Codex session log could not be opened.");

    // --- Nothing to show (AC-7)

    [Fact]
    public void NotFoundAndNoReadingYetEachHaveTheirOwnPhrase()
    {
        // Not installed and installed-but-unused call for different things from the user.
        var notFound = Assert.IsType<HudContent.Empty>(Section(
            new CodexState(CodexPresence.NotFound, null, null, TimeSpan.Zero, null, [])));
        var noReading = Assert.IsType<HudContent.Empty>(Section(Found(null)));

        Assert.False(string.IsNullOrWhiteSpace(notFound.Phrase));
        Assert.False(string.IsNullOrWhiteSpace(noReading.Phrase));
        Assert.NotEqual(notFound.Phrase, noReading.Phrase);
    }

    [Fact]
    public void BeforeTheFirstLookNothingIsClaimedAboutCodex()
    {
        // Not "not found": nothing has been looked at yet.
        var notLooked = Assert.IsType<HudContent.Empty>(Section(
            new CodexState(CodexPresence.NotLookedYet, null, null, TimeSpan.Zero, null, [])));
        var notFound = Assert.IsType<HudContent.Empty>(Section(
            new CodexState(CodexPresence.NotFound, null, null, TimeSpan.Zero, null, [])));

        Assert.NotEqual(notFound.Phrase, notLooked.Phrase);
    }

    // --- The age (AC-3)

    [Fact]
    public void AReadingAlwaysCarriesItsAge()
    {
        Assert.Equal("used 12 min ago", Reading(Found(Snapshot(), ageInMinutes: 12)).Mark);
    }

    [Fact]
    public void AReadingUnderAMinuteOldSaysJustNow()
    {
        Assert.Equal("used just now", Reading(Found(Snapshot(), ageInMinutes: 0.5)).Mark);
    }

    // --- Idle and failed (AC-3a)

    [Fact]
    public void AnOldReadingWithTheLogsReadCleanlyKeepsItsColours()
    {
        // Idle: Codex has not been used for three hours, so the figure is old and still true.
        var reading = Reading(Found(Snapshot(session: 80), ageInMinutes: 180));

        Assert.Equal("used 3 hours ago", reading.Mark);
        Assert.False(reading.Frozen);
        Assert.Equal(Severity.Warning, reading.Lines[0].Severity);
    }

    [Fact]
    public void AReadingTheLogsCannotConfirmIsMarkedAndLosesItsColours()
    {
        // Failed: Bingo cannot tell whether Codex has been used since, so the figure is drawn
        // like a frozen Claude reading, with the reason beside its age.
        var reading = Reading(Found(Snapshot(session: 80), ageInMinutes: 12, problem: Locked));

        Assert.Equal("used 12 min ago, logs unreadable", reading.Mark);
        Assert.True(reading.Frozen);
        Assert.All(reading.Lines, line => Assert.Equal(Severity.Normal, line.Severity));
    }

    [Fact]
    public void ARecordInAShapeBingoDoesNotKnowIsSaidDifferently()
    {
        var reading = Reading(Found(Snapshot(), problem: new CodexProblem.UnknownShape("The primary window had no percentage.")));

        Assert.Equal("used 12 min ago, log format not recognised", reading.Mark);
        Assert.True(reading.Frozen);
    }

    [Fact]
    public void TheMarksForTheTwoStatesDiffer()
    {
        Assert.NotEqual(
            Reading(Found(Snapshot())).Mark,
            Reading(Found(Snapshot(), problem: Locked)).Mark);
    }

    // --- A window whose reset has passed (AC-3, AC-2)

    [Fact]
    public void AWindowWhoseResetHasPassedShowsNoFigure()
    {
        var settings = UserSettings.Default with { ShowBar = true };
        var reading = Reading(Found(Snapshot(session: 95, sessionResetInMinutes: -20)), settings);

        Assert.Equal(
            new ReadoutLine("Codex 5h", Percent: null, "reset, no use since", Severity.Normal, Bar: null),
            reading.Lines[0]);
    }

    [Fact]
    public void AResetAtExactlyNowHasPassed()
    {
        var reading = Reading(Found(Snapshot(sessionResetInMinutes: 0)));

        Assert.Null(reading.Lines[0].Percent);
    }

    [Fact]
    public void TheOtherWindowStillShowsItsFigure()
    {
        var reading = Reading(Found(Snapshot(weekly: 20, sessionResetInMinutes: -20)));

        Assert.Equal(new ReadoutLine("Codex Week", "20% used", "resets Wed 11:34 AM", Severity.Normal, Bar: null), reading.Lines[1]);
    }

    [Fact]
    public void AWindowWhoseResetHasPassedCannotOwnTheAccent()
    {
        var hud = Readout.Compose(NoClaude, Found(Snapshot(session: 95, weekly: 20, sessionResetInMinutes: -20)),
            UserSettings.Default with { Products = new ProductSwitches(false, true) }, Now, TwelveHour);

        Assert.Equal(Severity.Normal, hud.Overall);
    }

    [Fact]
    public void CollapsePassesOverAWindowWhoseResetHasPassed()
    {
        // The ended window held 95%, but nothing about it needs attention now. Collapse keeps
        // the window that does have a figure.
        var settings = UserSettings.Default with { Collapse = true };
        var reading = Reading(Found(Snapshot(session: 95, weekly: 20, sessionResetInMinutes: -20)), settings);

        Assert.Equal("Codex Week", Assert.Single(reading.Lines).Window);
    }
}
