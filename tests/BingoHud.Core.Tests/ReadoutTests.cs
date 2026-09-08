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

    private static IReadOnlyList<ReadoutLine> Lines(
        QuotaSnapshot snapshot,
        DisplayDirection direction = DisplayDirection.Consumed) =>
        Readout.Lines(Fresh(snapshot), direction, Now, TwelveHour);

    private static IReadOnlyList<ReadoutLine> Lines(ReadingState state) =>
        Readout.Lines(state, DisplayDirection.Consumed, Now, TwelveHour);

    // ---- What may be shown at all (constitution principle 6, AC-8, AC-10, AC-13) ----

    [Fact]
    public void NoReadingYetMeansNoLines()
    {
        var nothing = new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "test");

        Assert.Empty(Lines(nothing));
    }

    [Fact]
    public void AFrozenReadingShowsNoPercentages()
    {
        // The token was invalidated after a good poll. The number is still in memory, but
        // it will never refresh, and a percentage that cannot move is one that will be read
        // as current. AC-10 and AC-13: an explicit state and no percentages; 7.1 writes the
        // words.
        var frozen = new ReadingState(
            Snapshot(Window(WindowKind.Session, 12)),
            Freshness.Frozen,
            new FetchOutcome.AuthFailed(AuthFailureKind.Invalidated),
            TimeSpan.FromMinutes(3),
            "test");

        Assert.Empty(Lines(frozen));
    }

    [Fact]
    public void AStaleReadingShowsNoPercentages()
    {
        // Older than a missed poll. Principle 6 says say so and show nothing rather than let
        // a number that may be an hour out sit there looking current; AC-8's age line is 7.1's.
        var stale = new ReadingState(
            Snapshot(Window(WindowKind.Session, 12)),
            Freshness.Stale,
            null,
            QuotaMonitor.StaleAfter + TimeSpan.FromMinutes(1),
            "test");

        Assert.Empty(Lines(stale));
    }

    [Fact]
    public void AFreshReadingStillShowsAfterAPassingFailure()
    {
        // One 503 after a good poll. The reading is minutes old and the next attempt is
        // already scheduled; hiding it would blank the HUD on every blip.
        var blip = Fresh(Snapshot(Window(WindowKind.Session, 12)), new FetchOutcome.Transient(null));

        Assert.Single(Lines(blip));
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

        var line = Assert.Single(Readout.Lines(Fresh(snapshot), DisplayDirection.Consumed, Now, new CultureInfo("de-DE")));

        Assert.Equal("resets Do 11:34", line.Reset);
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

        Assert.Null(line.Reset);
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
}
