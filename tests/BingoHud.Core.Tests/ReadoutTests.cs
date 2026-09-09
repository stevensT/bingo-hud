using System.Globalization;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The two lines the HUD shows (AC-1, AC-2, AC-2a, AC-2b, AC-3).
///
/// <para>
/// Every word on the HUD is decided here, in Core, and the WPF layer only places the strings.
/// That is the one hard rule of the plan, and the readout is where it pays off most: the
/// direction a percentage reads in is the single error nobody could spot by looking at the
/// screen, so it is pinned by a test instead.
/// </para>
/// </summary>
public class ReadoutTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");

    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static QuotaWindow Window(WindowKind kind, double used, DateTimeOffset? resetsAt = null) =>
        new(kind, used, resetsAt, ServerSeverity.Normal);

    private static QuotaSnapshot Snapshot(params QuotaWindow[] windows) =>
        new(windows, Now, RawBody: "{}");

    private static ReadingState Fresh(QuotaSnapshot snapshot, FetchOutcome? lastFailure = null) =>
        new(snapshot, Freshness.Fresh, lastFailure, Age: TimeSpan.Zero, PollReason: "test");

    private static UserSettings Settings(
        DisplayDirection direction = DisplayDirection.Consumed,
        bool collapse = false) =>
        UserSettings.Default with { Direction = direction, Collapse = collapse };

    private static IReadOnlyList<ReadoutLine> Lines(
        QuotaSnapshot snapshot,
        DisplayDirection direction = DisplayDirection.Consumed,
        bool collapse = false) =>
        Readout.Lines(Fresh(snapshot), Settings(direction, collapse), Now, TwelveHour);

    private static IReadOnlyList<ReadoutLine> Lines(ReadingState state) =>
        Readout.Lines(state, Settings(), Now, TwelveHour);

    // ---- What may be shown at all (constitution principle 6, AC-8, AC-10, AC-13) ----

    [Fact]
    public void NoReadingYetMeansNoLines()
    {
        var nothing = new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "test");

        Assert.Empty(Lines(nothing));
    }

    [Fact]
    public void AFrozenReadingKeepsItsNumbersAndSaysWhyTheyWillNotMove()
    {
        // The token was invalidated after a good poll. The number is real and still the last
        // thing the server said, so it stays; what it must never do is read as current. The mark
        // is what prevents that, and it names the cause rather than an age, because no newer
        // reading is coming until the user signs in (AC-10, AC-13).
        var frozen = new ReadingState(
            Snapshot(Window(WindowKind.Session, 12, Now.AddMinutes(53))),
            Freshness.Frozen,
            new FetchOutcome.AuthFailed(AuthFailureKind.Invalidated),
            TimeSpan.FromMinutes(3),
            "test");

        Assert.Equal(
            [new ReadoutLine("5h", "12% used", "frozen, sign-in expired")],
            Lines(frozen));
    }

    [Fact]
    public void AStaleReadingKeepsItsNumbersAndCarriesItsAge()
    {
        // AC-8, in the words the criterion asks for. A reading that missed a poll is still the
        // best thing known, and an age beside it is what makes it honest rather than hidden.
        var stale = new ReadingState(
            Snapshot(Window(WindowKind.Session, 12, Now.AddMinutes(53))),
            Freshness.Stale,
            null,
            TimeSpan.FromMinutes(48),
            "test");

        Assert.Equal(
            [new ReadoutLine("5h", "12% used", "48 min old")],
            Lines(stale));
    }

    [Fact]
    public void ANonCurrentLineGivesUpItsResetCountdownToTheMark()
    {
        // The countdown is the part that reads as live: it moves every second whether or not
        // anything behind it is still being fetched. A frozen percentage beside a ticking
        // "resets in 52 min" is precisely the display principle 6 forbids, so the mark takes
        // that slot rather than sitting next to it. The exact reset time is still in the panel.
        var frozen = new ReadingState(
            Snapshot(Window(WindowKind.Session, 12, Now.AddMinutes(53))),
            Freshness.Frozen,
            new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
            TimeSpan.FromMinutes(3),
            "test");

        Assert.DoesNotContain("resets", Lines(frozen).Single().Note);
    }

    [Fact]
    public void AFreshReadingStillShowsAfterAPassingFailure()
    {
        // One 503 after a good poll. The reading is minutes old and the next attempt is
        // already scheduled; hiding it would blank the HUD on every blip.
        var blip = Fresh(Snapshot(Window(WindowKind.Session, 12)), new FetchOutcome.Transient(null));

        Assert.Single(Lines(blip));
    }

    // ---- What replaces the lines when there are none ----

    [Fact]
    public void BeforeTheFirstPollTheHudSaysThereIsNoReadingRatherThanNothing()
    {
        // An empty HUD is indistinguishable from a broken one. Every launch passes through this
        // state, so it has to read as a normal thing rather than as a failure.
        var content = Readout.Content(
            new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "test"),
            Settings(),
            Now,
            TwelveHour);

        Assert.Empty(content.Lines);
        Assert.Equal("No reading yet", content.EmptyState);
    }

    [Fact]
    public void WithNoReadingTheEmptyStateNamesTheFailureRatherThanTheAbsence()
    {
        // AC-10. A user who has never signed in gets no reading at all, so the failure has
        // nowhere to appear except in place of the lines. "No reading yet" here would be true
        // and useless: it would never change, and it would not say why.
        var content = Readout.Content(
            new ReadingState(
                null,
                Freshness.Fresh,
                new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
                TimeSpan.Zero,
                "test"),
            Settings(),
            Now,
            TwelveHour);

        Assert.Empty(content.Lines);
        Assert.Equal("Signed out", content.EmptyState);
    }

    [Fact]
    public void WhileThereAreLinesThereIsNoEmptyStatePhrase()
    {
        // The lines carry their own status in the mark. A headline above them would say a second
        // time what each line already says, on the display with the least room to spare.
        var content = Readout.Content(
            Fresh(Snapshot(Window(WindowKind.Session, 12))),
            Settings(),
            Now,
            TwelveHour);

        Assert.Single(content.Lines);
        Assert.Null(content.EmptyState);
    }

    // ---- The lines themselves (AC-1 through AC-3) ----

    [Fact]
    public void BothWindowsAppearWithTheirPercentAndReset()
    {
        var snapshot = Snapshot(
            Window(WindowKind.Session, 12, Now.AddMinutes(53)),
            Window(WindowKind.WeeklyAll, 37, Now.AddDays(3).AddHours(2)));

        Assert.Equal(
            [
                new ReadoutLine("5h", "12% used", "resets in 53 min"),
                new ReadoutLine("Week", "37% used", "resets Thu 11:34 AM"),
            ],
            Lines(snapshot));
    }

    [Fact]
    public void ThePercentIsConsumedByDefault()
    {
        // AC-2: what /usage reports, so the two never need reconciling in the reader's head.
        var line = Assert.Single(Lines(Snapshot(Window(WindowKind.Session, 88))));

        Assert.Equal("88% used", line.Percent);
    }

    [Fact]
    public void RemainingInvertsThePercentAndSaysSo()
    {
        // AC-2a and AC-2b together: the figure changes, and the word beside it changes with it.
        var line = Assert.Single(Lines(
            Snapshot(Window(WindowKind.Session, 88)),
            DisplayDirection.Remaining));

        Assert.Equal("12% left", line.Percent);
    }

    [Fact]
    public void TheDirectionIsNamedInBothSettings()
    {
        // AC-2b. A bare percentage whose meaning depends on a setting the reader cannot see
        // is a number that can be read exactly backwards.
        var snapshot = Snapshot(Window(WindowKind.Session, 50));

        Assert.EndsWith("used", Assert.Single(Lines(snapshot, DisplayDirection.Consumed)).Percent);
        Assert.EndsWith("left", Assert.Single(Lines(snapshot, DisplayDirection.Remaining)).Percent);
    }

    [Fact]
    public void AFractionalPercentIsRoundedToTheNearestWholeNumber()
    {
        var snapshot = Snapshot(Window(WindowKind.Session, 62.6));

        Assert.Equal("63% used", Assert.Single(Lines(snapshot)).Percent);
        Assert.Equal("37% left", Assert.Single(Lines(snapshot, DisplayDirection.Remaining)).Percent);
    }

    [Fact]
    public void TheTwoDirectionsSumToOneHundredAtAMidpoint()
    {
        // 62.5 rounds up to 63 used, and 37.5 would round up to 38 left: 101 between them.
        // The figure is rounded once and inverted as a whole number, so the two directions are
        // always the same reading described two ways.
        var snapshot = Snapshot(Window(WindowKind.Session, 62.5));

        Assert.Equal("63% used", Assert.Single(Lines(snapshot)).Percent);
        Assert.Equal("37% left", Assert.Single(Lines(snapshot, DisplayDirection.Remaining)).Percent);
    }

    [Fact]
    public void AnUntouchedWindowReadsZeroUsedAndOneHundredLeft()
    {
        var snapshot = Snapshot(Window(WindowKind.Session, 0));

        Assert.Equal("0% used", Assert.Single(Lines(snapshot)).Percent);
        Assert.Equal("100% left", Assert.Single(Lines(snapshot, DisplayDirection.Remaining)).Percent);
    }

    [Fact]
    public void AnExhaustedWindowReadsOneHundredUsedAndZeroLeft()
    {
        var snapshot = Snapshot(Window(WindowKind.Session, 100));

        Assert.Equal("100% used", Assert.Single(Lines(snapshot)).Percent);
        Assert.Equal("0% left", Assert.Single(Lines(snapshot, DisplayDirection.Remaining)).Percent);
    }

    [Fact]
    public void TheResetPhraseUsesTheCultureTheCallerGave()
    {
        // ResetFormatter honours culture; this pins that Readout hands it on rather than
        // letting the phrase fall back to whatever the process culture happens to be.
        var snapshot = Snapshot(Window(WindowKind.WeeklyAll, 37, Now.AddDays(3).AddHours(2)));

        var line = Assert.Single(Readout.Lines(Fresh(snapshot), Settings(), Now, new CultureInfo("de-DE")));

        Assert.Equal("resets Do 11:34", line.Note);
    }

    [Fact]
    public void ASnapshotWithOnlyModelScopedWindowsYieldsNoLines()
    {
        Assert.Empty(Lines(Snapshot(Window(WindowKind.WeeklyScoped, 5))));
    }

    [Fact]
    public void AWindowWithNoResetTimeHasNoResetPhrase()
    {
        // Observed on a live account. Nothing beside the percentage, never a guessed time.
        var line = Assert.Single(Lines(Snapshot(Window(WindowKind.Session, 12))));

        Assert.Null(line.Note);
    }

    [Fact]
    public void AWindowTheServerDidNotReportIsAbsentRatherThanZero()
    {
        // Constitution principle 6: a line for a window nothing backs would be a zero on screen
        // that the server never sent.
        var lines = Lines(Snapshot(Window(WindowKind.WeeklyAll, 37)));

        Assert.Equal("Week", Assert.Single(lines).Window);
    }

    [Fact]
    public void TheSessionLineComesFirstWhateverOrderTheServerUsed()
    {
        var lines = Lines(Snapshot(
            Window(WindowKind.WeeklyAll, 37),
            Window(WindowKind.Session, 12)));

        Assert.Equal(["5h", "Week"], lines.Select(line => line.Window));
    }

    [Fact]
    public void AModelScopedWeeklyWindowBelongsToTheDetailPanelNotTheHud()
    {
        // AC-1 names the five-hour and weekly windows. Per-model caps are AC-23's and arrive
        // with the detail panel; on the HUD they would be a third line nobody asked for.
        var lines = Lines(Snapshot(
            Window(WindowKind.Session, 12),
            Window(WindowKind.WeeklyAll, 37),
            Window(WindowKind.WeeklyScoped, 5)));

        Assert.Equal(["5h", "Week"], lines.Select(line => line.Window));
    }

    // ---- Collapse (AC-7) ----

    [Fact]
    public void BothWindowsAreShownByDefault()
    {
        // The default is not a preference, it is the honest state: two windows exist, so two
        // windows are named. Collapse is the user opting into less.
        var snapshot = Snapshot(Window(WindowKind.Session, 30), Window(WindowKind.WeeklyAll, 60));

        Assert.Equal(2, Lines(snapshot).Count);
    }

    [Fact]
    public void CollapseShowsOnlyTheFullerWindowWhenBothAreNormal()
    {
        var snapshot = Snapshot(Window(WindowKind.Session, 30), Window(WindowKind.WeeklyAll, 60));

        var line = Assert.Single(Lines(snapshot, collapse: true));

        Assert.Equal("Week", line.Window);
        Assert.Equal("60% used", line.Percent);
    }

    [Fact]
    public void CollapseShowsOnlyTheWindowThatIsInTrouble()
    {
        // Session at 20% remaining has crossed the warning line; the weekly window has not.
        // Severity decides, so the fuller-looking window does not win by percentage alone.
        var snapshot = Snapshot(Window(WindowKind.Session, 80), Window(WindowKind.WeeklyAll, 10));

        var line = Assert.Single(Lines(snapshot, collapse: true));

        Assert.Equal("5h", line.Window);
    }

    [Fact]
    public void CollapseShowsBothWindowsWhenBothAreInTrouble()
    {
        // The one case collapse gives way. Hiding either window here would hide a limit the
        // user is about to hit, which is the opposite of what the HUD is for.
        var snapshot = Snapshot(Window(WindowKind.Session, 80), Window(WindowKind.WeeklyAll, 95));

        Assert.Equal(2, Lines(snapshot, collapse: true).Count);
    }

    [Fact]
    public void CollapsePrefersTheWindowTheServerIsRefusing()
    {
        // The weekly window looks the healthier of the two by percentage. The server refusing
        // work against it outranks that, because it is a fact rather than an opinion (AC-6).
        var snapshot = Snapshot(
            Window(WindowKind.Session, 70),
            new QuotaWindow(WindowKind.WeeklyAll, 20, null, ServerSeverity.Rejected));

        var line = Assert.Single(Lines(snapshot, collapse: true));

        Assert.Equal("Week", line.Window);
    }

    [Fact]
    public void CollapseBreaksAnExactTieTowardTheSessionWindow()
    {
        // Equal severity and equal percentage. The session window is the one that stops work
        // first, so it is the one worth the single line.
        var snapshot = Snapshot(Window(WindowKind.Session, 40), Window(WindowKind.WeeklyAll, 40));

        var line = Assert.Single(Lines(snapshot, collapse: true));

        Assert.Equal("5h", line.Window);
    }

    [Fact]
    public void CollapseLeavesASingleReportedWindowAlone()
    {
        // Nothing to choose between. A window the server did not report still has no line, and
        // collapse must not turn the one that was reported into none.
        var snapshot = Snapshot(Window(WindowKind.Session, 30));

        var line = Assert.Single(Lines(snapshot, collapse: true));

        Assert.Equal("5h", line.Window);
    }
}
