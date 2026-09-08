using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// What the panel says after a manual refresh (AC-28).
///
/// <para>
/// The criterion is specific about a refusal: it has to say why, and when the next attempt is
/// possible. Both halves matter. "Try again later" tells a user nothing they can act on, and a
/// refresh button that appears to do nothing reads as a broken button rather than as a floor
/// being enforced.
/// </para>
/// <para>
/// A refusal is a normal outcome here, not an error. Bingo polls no faster than its floor
/// whoever asks, and saying so plainly is the honest thing — the alternative is a button that
/// quietly lies about having refreshed.
/// </para>
/// </summary>
public class RefreshNoticeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 14, 30, 0, TimeSpan.FromHours(-7));

    private static RefreshResult.Refused Refused(TimeSpan until, string reason) =>
        new(reason, Now + until);

    [Fact]
    public void NothingIsSaidBeforeAnyRefreshHasBeenAsked()
    {
        // The notice is a response to an action. A line sitting there before the button has been
        // touched would be answering a question nobody asked.
        Assert.Null(RefreshNotice.Describe(null, Now));
    }

    [Fact]
    public void ASuccessfulRefreshSaysNothing()
    {
        // The reading's age resets to "just now" and the numbers move if they changed. That is
        // the feedback, and a second line claiming success would only repeat it.
        var performed = new RefreshResult.Performed(
            new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "test"));

        Assert.Null(RefreshNotice.Describe(performed, Now));
    }

    [Fact]
    public void ARefusalSaysBothWhenAndWhy()
    {
        var notice = RefreshNotice.Describe(
            Refused(TimeSpan.FromMinutes(3), PollPolicy.Reasons.ClaudeCodeIsWorking),
            Now);

        Assert.Equal("Not yet. Next attempt in 3 min, because Claude Code is working.", notice);
    }

    [Fact]
    public void ARefusalCarriesWhicheverReasonSetTheInterval()
    {
        // The reason comes from the cadence policy, so every reason it can choose has to read as
        // a sentence here. This is the one a user is most likely to be surprised by.
        var notice = RefreshNotice.Describe(
            Refused(TimeSpan.FromMinutes(12), PollPolicy.Reasons.ServerAskedToWait),
            Now);

        Assert.Equal("Not yet. Next attempt in 12 min, because the server asked us to wait.", notice);
    }

    [Fact]
    public void ARefusalUnderAMinuteAwayDoesNotSayZeroMinutes()
    {
        // "in 0 min" reads as though the attempt has already happened.
        var notice = RefreshNotice.Describe(
            Refused(TimeSpan.FromSeconds(20), PollPolicy.Reasons.NothingIsHappening),
            Now);

        Assert.Equal(
            "Not yet. Next attempt in under a minute, because nothing is happening.",
            notice);
    }

    [Fact]
    public void ARefusalWhoseMomentHasArrivedSaysSoRatherThanCountingBackwards()
    {
        // Reachable: the refusal is composed at one instant and read at the next, and a clock
        // that crossed the boundary in between would otherwise produce a negative countdown.
        var notice = RefreshNotice.Describe(
            Refused(TimeSpan.FromSeconds(-5), PollPolicy.Reasons.NothingIsHappening),
            Now);

        Assert.Equal(
            "Not yet. Next attempt any moment, because nothing is happening.",
            notice);
    }

    [Theory]
    [InlineData("the server asked us to wait")]
    [InlineData("the last attempt failed")]
    [InlineData("the machine is on battery")]
    [InlineData("the panel is open")]
    [InlineData("Claude Code is working")]
    [InlineData("nothing is happening")]
    public void EveryReasonTheCadencePolicyCanChooseReadsAsASentence(string reason)
    {
        // Listed by value rather than reflected out of the policy, so a new reason worded as a
        // noun phrase rather than a clause fails here instead of appearing mid-sentence on screen.
        var notice = RefreshNotice.Describe(Refused(TimeSpan.FromMinutes(4), reason), Now);

        Assert.Equal($"Not yet. Next attempt in 4 min, because {reason}.", notice);
    }
}
