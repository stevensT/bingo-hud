using System.Globalization;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Everything the detail panel says (AC-23, AC-24).
///
/// <para>
/// Composed in Core for the same reason the HUD's two lines are: the panel is the screen where
/// the user goes to find out whether a number can be trusted, so what it claims has to be pinned
/// by tests rather than by looking at it. The WPF layer places these strings and decides none of
/// them.
/// </para>
/// <para>
/// The panel is where the honesty rules become visible. The HUD blanks a stale reading; the
/// panel has to show it, with its age, because "why is the HUD empty" is exactly the question
/// the panel exists to answer.
/// </para>
/// </summary>
public class PanelReadoutTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");

    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 14, 30, 0, TimeSpan.FromHours(-7));

    private const string Version = "0.1.0";

    private static QuotaWindow Window(
        WindowKind kind,
        double used,
        DateTimeOffset? resetsAt = null,
        string? scope = null) =>
        new(kind, used, resetsAt, ServerSeverity.Normal, scope);

    private static QuotaSnapshot Snapshot(DateTimeOffset observedAt, params QuotaWindow[] windows) =>
        new(windows, observedAt, RawBody: "{}");

    private static QuotaSnapshot Snapshot(params QuotaWindow[] windows) =>
        Snapshot(Now - TimeSpan.FromMinutes(3), windows);

    private static ReadingState Reading(
        QuotaSnapshot? snapshot,
        Freshness freshness = Freshness.Fresh,
        TimeSpan? age = null,
        string pollReason = "steady state") =>
        new(snapshot, freshness, null, age ?? TimeSpan.FromMinutes(3), pollReason);

    private static PanelContent Compose(ReadingState state) =>
        PanelReadout.Compose(state, UserSettings.Default, Version, Now, TwelveHour);

    // ---- What the panel is for: the version and the last poll (AC-24) ----

    [Fact]
    public void ThePanelNamesTheRunningVersion()
    {
        // When the payload drifts, "which build is misreading it" has to be answerable from the
        // screen, by a user who cannot be asked to run a command.
        Assert.Equal("0.1.0", Compose(Reading(Snapshot(Window(WindowKind.Session, 12)))).Version);
    }

    [Fact]
    public void ThePanelGivesTheLastSuccessfulPollAsAnExactTime()
    {
        // Not "3 minutes ago". The panel is the place a user checks a number against reality,
        // and a relative phrase cannot be compared against anything.
        var snapshot = Snapshot(
            new DateTimeOffset(2026, 9, 5, 14, 27, 0, TimeSpan.FromHours(-7)),
            Window(WindowKind.Session, 12));

        Assert.Equal("Sat 5 Sep 2026, 2:27 PM", Compose(Reading(snapshot)).LastPoll);
    }

    [Fact]
    public void ThePanelSaysSoWhenNoPollHasEverSucceeded()
    {
        // Never polled is not the same as polled and empty, and a blank here would read as the
        // second. The panel has to distinguish them.
        Assert.Equal("never", Compose(Reading(null)).LastPoll);
    }

    [Fact]
    public void ThePanelCarriesTheReasonTheNextPollIsWhenItIs()
    {
        var state = Reading(Snapshot(Window(WindowKind.Session, 12)), pollReason: "on battery");

        Assert.Equal("on battery", Compose(state).NextPoll);
    }

    // ---- Windows, with exact reset times (AC-23) ----

    [Fact]
    public void ThePanelShowsBothWindowsWithTheirPercentages()
    {
        var snapshot = Snapshot(
            Window(WindowKind.Session, 12),
            Window(WindowKind.WeeklyAll, 37));

        var rows = Compose(Reading(snapshot)).Windows;

        Assert.Equal(["5h", "Week"], rows.Select(r => r.Label));
        Assert.Equal(["12% used", "37% used"], rows.Select(r => r.Percent));
    }

    [Fact]
    public void APanelResetTimeIsExactRatherThanACountdown()
    {
        // The HUD counts down because it has one line and no room. The panel has room, and the
        // exact time is the thing a user can check against their own clock.
        var resets = new DateTimeOffset(2026, 9, 5, 14, 45, 0, TimeSpan.FromHours(-7));
        var snapshot = Snapshot(Window(WindowKind.Session, 12, resets));

        var row = Assert.Single(Compose(Reading(snapshot)).Windows);

        Assert.Equal("Sat 5 Sep 2026, 2:45 PM", row.Reset);
    }

    [Fact]
    public void AWindowWithNoResetTimeSaysSoRatherThanGuessingOne()
    {
        var snapshot = Snapshot(Window(WindowKind.Session, 12, resetsAt: null));

        var row = Assert.Single(Compose(Reading(snapshot)).Windows);

        Assert.Equal("not reported", row.Reset);
    }

    [Fact]
    public void ThePanelShowsAStaleReadingRatherThanHidingIt()
    {
        // The HUD blanks a stale reading, because a percentage beside a moving countdown reads
        // as current. The panel is where the user goes to ask why, so blanking it here would
        // leave that question unanswerable.
        var state = Reading(
            Snapshot(Window(WindowKind.Session, 12)),
            Freshness.Stale,
            age: TimeSpan.FromMinutes(21));

        Assert.Single(Compose(state).Windows);
    }

    [Fact]
    public void ThePanelShowsAFrozenReadingRatherThanHidingIt()
    {
        var state = Reading(
            Snapshot(Window(WindowKind.Session, 12)),
            Freshness.Frozen,
            age: TimeSpan.FromHours(2));

        Assert.Single(Compose(state).Windows);
    }

    [Fact]
    public void ThePanelGivesTheAgeOfTheReadingItIsShowing()
    {
        // A number on this screen without its age is the exact thing principle 6 forbids, and
        // the panel is the one place the age is always shown rather than only once it matters.
        var state = Reading(
            Snapshot(Window(WindowKind.Session, 12)),
            age: TimeSpan.FromMinutes(21));

        Assert.Equal("21 min old", Compose(state).Age);
    }

    [Fact]
    public void ThereIsNoAgeToGiveBeforeTheFirstReading()
    {
        Assert.Null(Compose(Reading(null)).Age);
    }

    [Fact]
    public void ThePanelReadsPercentagesTheWayTheUserAskedFor()
    {
        var settings = UserSettings.Default with { Direction = DisplayDirection.Remaining };
        var state = Reading(Snapshot(Window(WindowKind.Session, 12)));

        var content = PanelReadout.Compose(state, settings, Version, Now, TwelveHour);

        Assert.Equal("88% left", Assert.Single(content.Windows).Percent);
    }

    // ---- Per-model weekly caps: the empty state is the common case (AC-23) ----

    [Fact]
    public void PerModelCapsAreEmptyOnEveryPayloadObservedSoFar()
    {
        var snapshot = Snapshot(
            Window(WindowKind.Session, 12),
            Window(WindowKind.WeeklyAll, 37));

        Assert.Empty(Compose(Reading(snapshot)).PerModelCaps);
    }

    [Fact]
    public void AnEmptyPerModelSectionSaysTheAccountHasNoneRatherThanShowingNothing()
    {
        // Every capture so far reports these as null, so this is the state a user will almost
        // always see. An empty area under a heading reads as a screen that failed to load;
        // saying none were reported is the difference between an empty state and a bug.
        var snapshot = Snapshot(Window(WindowKind.Session, 12));

        Assert.Equal(
            "None reported for this account.",
            Compose(Reading(snapshot)).PerModelCapsEmptyState);
    }

    [Fact]
    public void APerModelCapIsLabelledWithTheScopeTheServerSent()
    {
        var snapshot = Snapshot(
            Window(WindowKind.Session, 12),
            Window(WindowKind.WeeklyScoped, 40, scope: "claude-opus-4"));

        var row = Assert.Single(Compose(Reading(snapshot)).PerModelCaps);

        Assert.Equal("claude-opus-4", row.Label);
        Assert.Equal("40% used", row.Percent);
    }

    [Fact]
    public void APerModelCapDoesNotAppearAmongTheHudWindows()
    {
        var snapshot = Snapshot(
            Window(WindowKind.Session, 12),
            Window(WindowKind.WeeklyScoped, 40, scope: "claude-opus-4"));

        var row = Assert.Single(Compose(Reading(snapshot)).Windows);

        Assert.Equal("5h", row.Label);
    }

    [Fact]
    public void ThereIsNoEmptyStateWhenPerModelCapsWereReported()
    {
        var snapshot = Snapshot(Window(WindowKind.WeeklyScoped, 40, scope: "claude-opus-4"));

        Assert.Null(Compose(Reading(snapshot)).PerModelCapsEmptyState);
    }

    [Fact]
    public void ThePanelHasNoWindowsAtAllBeforeTheFirstReading()
    {
        var content = Compose(Reading(null));

        Assert.Empty(content.Windows);
        Assert.Empty(content.PerModelCaps);
    }
}
