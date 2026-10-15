using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The words for every Codex state that is not a plain reading (codex-quota AC-7, AC-3a).
///
/// <para>
/// The same rule as Claude's states: each says what happened and what to do about it. Here the
/// second half sends the user to different places. Not found is fixed by installing Codex, or by
/// switching it off if it is not wanted. No reading yet is fixed by using Codex once. Logs Bingo
/// cannot open usually fix themselves. A record Bingo does not recognise will not fix itself,
/// and saying so is the point: the user should not wait on a retry that cannot help.
/// </para>
/// </summary>
public class CodexStatusMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private static readonly QuotaSnapshot Snapshot = new(
        [new QuotaWindow(WindowKind.Session, 30, Now.AddHours(2), ServerSeverity.Unknown)], Now, "{}");

    private static CodexState State(CodexPresence presence, bool reading = false, CodexProblem? problem = null) =>
        new(presence, reading ? Snapshot : null, problem, TimeSpan.FromMinutes(12), ReachedType: null, []);

    private static StatusMessage Describe(CodexState state)
    {
        var message = StatusMessage.DescribeCodex(state);
        Assert.NotNull(message);
        Assert.False(string.IsNullOrWhiteSpace(message.Headline));
        Assert.False(string.IsNullOrWhiteSpace(message.Advice));
        return message;
    }

    private static readonly CodexProblem Locked =
        new CodexProblem.LogsUnreadable("A Codex session log could not be opened, possibly because another program is holding it.");

    private static readonly CodexProblem Changed =
        new CodexProblem.UnknownShape("The primary window had no percentage.");

    [Fact]
    public void NotFoundSaysSoAndOffersBothWaysOut()
    {
        var message = Describe(State(CodexPresence.NotFound));

        Assert.Equal("Codex not found", message.Headline);
        Assert.Contains("CODEX_HOME", message.Advice, StringComparison.Ordinal);
        Assert.Contains("tray", message.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void NoReadingYetSaysToUseCodexOnce()
    {
        var message = Describe(State(CodexPresence.Found));

        Assert.Equal("No Codex reading yet", message.Headline);
        Assert.Contains("turn", message.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void BeforeTheFirstLookItSaysNothingIsKnownYet()
    {
        var message = Describe(State(CodexPresence.NotLookedYet));

        Assert.NotEqual("Codex not found", message.Headline);
        Assert.NotEqual("No Codex reading yet", message.Headline);
    }

    [Fact]
    public void LogsThatCannotBeOpenedCarryTheReasonAndExpectToRecover()
    {
        var message = Describe(State(CodexPresence.Found, reading: true, problem: Locked));

        Assert.Equal("Codex logs unreadable", message.Headline);
        Assert.Contains("another program is holding it", message.Advice, StringComparison.Ordinal);
        Assert.Contains("try again", message.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordBingoDoesNotRecogniseCarriesTheReasonAndSaysAnUpdateIsNeeded()
    {
        var message = Describe(State(CodexPresence.Found, reading: true, problem: Changed));

        Assert.Equal("Codex log format not recognised", message.Headline);
        Assert.Contains("The primary window had no percentage.", message.Advice, StringComparison.Ordinal);
        Assert.Contains("update", message.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void AProblemBeforeAnyReadingIsStillDescribed()
    {
        Assert.Equal("Codex logs unreadable", Describe(State(CodexPresence.Found, problem: Locked)).Headline);
    }

    [Fact]
    public void AReadingReadCleanlyHasNothingToSay()
    {
        Assert.Null(StatusMessage.DescribeCodex(State(CodexPresence.Found, reading: true)));
    }

    [Fact]
    public void TheHudsPhraseIsTheHeadline()
    {
        // One table for both surfaces, so a state cannot be named one way on the HUD and another
        // in the panel.
        foreach (var state in new[]
        {
            State(CodexPresence.NotFound),
            State(CodexPresence.Found),
            State(CodexPresence.NotLookedYet),
            State(CodexPresence.Found, problem: Locked),
            State(CodexPresence.Found, problem: Changed),
        })
        {
            var hud = Readout.Compose(
                new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "test"),
                state,
                UserSettings.Default with { Products = new ProductSwitches(Claude: false, Codex: true) },
                Now);

            var empty = Assert.IsType<HudContent.Empty>(Assert.Single(hud.Sections).Content);
            Assert.Equal(StatusMessage.DescribeCodex(state)!.Headline, empty.Phrase);
        }
    }
}
