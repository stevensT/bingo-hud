using System.Globalization;
using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The detail panel with Codex in it (codex-quota AC-1, AC-6, AC-8).
///
/// <para>
/// The panel is where the HUD is made accountable, so the Codex block carries what the HUD has no
/// room for: exact reset times, when the reading was taken, the advice for any problem, and the
/// one limit every Codex figure has, that it comes from this machine's logs and cannot see Codex
/// used anywhere else.
/// </para>
/// </summary>
public class CodexPanelTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));
    private static readonly DateTimeOffset TurnAt = Now.AddMinutes(-12);

    private static readonly ReadingState Claude = new(
        new QuotaSnapshot([new QuotaWindow(WindowKind.Session, 12, Now.AddMinutes(53), ServerSeverity.Normal)], Now, "{}"),
        Freshness.Fresh, null, TimeSpan.Zero, "test");

    private static QuotaSnapshot Snapshot(double sessionResetInMinutes = 53) => new(
        [
            new QuotaWindow(WindowKind.Session, 30, Now.AddMinutes(sessionResetInMinutes), ServerSeverity.Unknown),
            new QuotaWindow(WindowKind.WeeklyAll, 20, Now.AddDays(2), ServerSeverity.Unknown),
        ],
        TurnAt,
        "{}");

    private static CodexState Found(
        QuotaSnapshot? snapshot, CodexProblem? problem = null, string? reached = null, IReadOnlyList<string>? ignored = null) =>
        new(CodexPresence.Found, snapshot, problem, snapshot is null ? TimeSpan.Zero : Now - TurnAt, reached, ignored ?? []);

    private static CodexPanel Panel(CodexState state, UserSettings? settings = null) =>
        PanelReadout.ComposeCodex(state, settings ?? UserSettings.Default, Now, TwelveHour);

    private static PanelContent All(CodexState codex, bool claude, bool codexOn) =>
        PanelReadout.ComposeAll(
            Claude, codex, UserSettings.Default with { Products = new ProductSwitches(claude, codexOn) },
            "0.5.0", Now, TwelveHour);

    // --- Which blocks

    [Fact]
    public void CodexShownGivesTheCodexBlock()
    {
        Assert.NotNull(All(Found(Snapshot()), claude: true, codexOn: true).Codex);
    }

    [Fact]
    public void CodexSwitchedOffGivesNoCodexBlock()
    {
        Assert.Null(All(Found(Snapshot()), claude: true, codexOn: false).Codex);
    }

    [Fact]
    public void ClaudeSwitchedOffIsSaid()
    {
        var panel = All(Found(Snapshot()), claude: false, codexOn: true);

        Assert.False(panel.ShowClaude);
        Assert.NotNull(panel.Codex);
    }

    [Fact]
    public void BeforeTheQuestionIsAnsweredThePanelIsClaudesAlone()
    {
        var panel = PanelReadout.ComposeAll(Claude, Found(Snapshot()), UserSettings.Default, "0.5.0", Now, TwelveHour);

        Assert.True(panel.ShowClaude);
        Assert.Null(panel.Codex);
    }

    [Fact]
    public void TheClaudePartIsWhatItWasBefore()
    {
        // Claude's part of the panel does not change shape for Codex being beside it.
        var alone = PanelReadout.Compose(Claude, UserSettings.Default, "0.5.0", Now, TwelveHour);
        var together = All(Found(Snapshot()), claude: true, codexOn: true);

        Assert.True(alone.SameAs(together with { ShowClaude = true, Codex = null }));
    }

    // --- The rows

    [Fact]
    public void BothWindowsHaveTheirFigureAndExactReset()
    {
        var panel = Panel(Found(Snapshot()));

        Assert.Equal(
            [
                new PanelRow("5h", "30% used", ResetFormatter.Exact(Now.AddMinutes(53), Now, TwelveHour)),
                new PanelRow("Week", "20% used", ResetFormatter.Exact(Now.AddDays(2), Now, TwelveHour)),
            ],
            panel.Windows);
        Assert.Null(panel.WindowsEmptyState);
    }

    [Fact]
    public void TheRowsReadTheWayTheHudDoes()
    {
        var panel = Panel(Found(Snapshot()), UserSettings.Default with { Direction = DisplayDirection.Remaining });

        Assert.Equal("70% left", panel.Windows[0].Percent);
    }

    [Fact]
    public void AWindowWhoseResetHasPassedHasNoFigureAndSaysWhenItReset()
    {
        // The same rule as the HUD (AC-3): the figure belongs to an ended occurrence.
        var row = Panel(Found(Snapshot(sessionResetInMinutes: -20))).Windows[0];

        Assert.Equal("5h", row.Label);
        Assert.DoesNotContain("%", row.Percent, StringComparison.Ordinal);
        Assert.Equal(ResetFormatter.Exact(Now.AddMinutes(-20), Now, TwelveHour), row.Reset);
    }

    [Fact]
    public void NoReadingHasNoRowsAndSaysSo()
    {
        var panel = Panel(Found(null));

        Assert.Empty(panel.Windows);
        Assert.Equal("Nothing read yet.", panel.WindowsEmptyState);
        Assert.Null(panel.Age);
    }

    // --- When, and from where

    [Fact]
    public void TheAgeAndTheTurnItCameFromAreGiven()
    {
        var panel = Panel(Found(Snapshot()));

        Assert.Equal("12 min old", panel.Age);
        Assert.Equal(ResetFormatter.Exact(TurnAt, Now, TwelveHour), panel.LastTurn);
    }

    [Fact]
    public void BeforeAnyReadingTheLastTurnIsNever()
    {
        Assert.Equal("never", Panel(Found(null)).LastTurn);
    }

    [Fact]
    public void TheSourceAndWhatItCannotSeeAreAlwaysSaid()
    {
        // AC-1: the reading is as of the last Codex turn on this machine.
        foreach (var state in new[] { Found(Snapshot()), Found(null) })
        {
            var source = Panel(state).Source;

            Assert.Contains("last Codex turn on this machine", source, StringComparison.Ordinal);
            Assert.Contains("elsewhere", source, StringComparison.Ordinal);
        }
    }

    // --- Status and the rest

    [Fact]
    public void AProblemBringsItsHeadlineAndAdvice()
    {
        var problem = new CodexProblem.LogsUnreadable("A Codex session log could not be opened.");
        var state = Found(Snapshot(), problem);

        Assert.Equal(StatusMessage.DescribeCodex(state), Panel(state).Status);
    }

    [Fact]
    public void AReadingReadCleanlyHasNoStatus()
    {
        Assert.Null(Panel(Found(Snapshot())).Status);
    }

    [Fact]
    public void TheLimitReachedFieldIsShownAsWritten()
    {
        Assert.Equal("primary", Panel(Found(Snapshot(), reached: "primary")).ReachedType);
        Assert.Null(Panel(Found(Snapshot())).ReachedType);
    }

    [Fact]
    public void WindowsOfAnUnknownLengthAreListed()
    {
        Assert.Equal(["secondary (1440 minutes)"], Panel(Found(Snapshot(), ignored: ["secondary (1440 minutes)"])).IgnoredWindows);
    }

    // --- Redrawing

    [Fact]
    public void TheSameStateComposesTheSamePanel()
    {
        Assert.True(All(Found(Snapshot()), true, true).SameAs(All(Found(Snapshot()), true, true)));
    }

    [Fact]
    public void AChangeInTheCodexBlockIsADifferentPanel()
    {
        var before = All(Found(Snapshot()), true, true);

        Assert.False(before.SameAs(All(Found(Snapshot(sessionResetInMinutes: 60)), true, true)));
        Assert.False(before.SameAs(All(Found(Snapshot()), true, false)));
    }
}
