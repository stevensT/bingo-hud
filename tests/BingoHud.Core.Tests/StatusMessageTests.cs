using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The words for every state that is not a plain current reading (AC-9, AC-10, AC-11).
///
/// <para>
/// Each state has to say two things: what happened, and what to do about it. The second half is
/// the one that is easy to leave out and the one AC-11 turns on. "Sign-in failed" is true of both
/// a missing credential and a credential Windows will not open, and a user told to sign in again
/// when the real problem is file permissions will do it, watch nothing change, and have no idea
/// why. Distinguishing them is the whole point of the criterion.
/// </para>
/// <para>
/// A third field, the mark, is what goes beside a number the HUD is still showing. Stale and
/// frozen readings stay on screen carrying their status, so the mark is what stops them being
/// read as current.
/// </para>
/// </summary>
public class StatusMessageTests
{
    private static QuotaSnapshot Snapshot() =>
        new(
            [new QuotaWindow(WindowKind.Session, 83, null, ServerSeverity.Normal)],
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.FromHours(-7)),
            RawBody: "{}");

    private static ReadingState State(
        FetchOutcome? failure = null,
        Freshness freshness = Freshness.Fresh,
        TimeSpan? age = null,
        bool hasReading = true) =>
        new(hasReading ? Snapshot() : null, freshness, failure, age ?? TimeSpan.Zero, "test");

    private static StatusMessage Describe(
        FetchOutcome? failure = null,
        Freshness freshness = Freshness.Fresh,
        TimeSpan? age = null,
        bool hasReading = true)
    {
        var message = StatusMessage.Describe(State(failure, freshness, age, hasReading));

        Assert.NotNull(message);

        return message;
    }

    // ---- Nothing to say ----

    [Fact]
    public void ACurrentReadingWithNothingWrongSaysNothing()
    {
        // The numbers are the message. A status line reading "everything is fine" beside them is
        // noise on a display whose whole purpose is to be glanced at.
        Assert.Null(StatusMessage.Describe(State()));
    }

    // ---- Signing in (AC-10, AC-11) ----

    [Fact]
    public void BeingSignedOutSendsTheUserToTheCommandThatSignsThemIn()
    {
        var message = Describe(new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut));

        Assert.Equal("Signed out", message.Headline);
        Assert.Contains("claude", message.Advice);
    }

    [Fact]
    public void ARejectedTokenAsksForAFreshSignInRatherThanReportingAFault()
    {
        // An expired token is a daily event, not a breakage. The words should read like a
        // routine thing to do, because it is one.
        var message = Describe(new FetchOutcome.AuthFailed(AuthFailureKind.Invalidated));

        Assert.Equal("Sign-in expired", message.Headline);
        Assert.Contains("claude", message.Advice);
    }

    [Fact]
    public void APermissionFailureDoesNotTellTheUserToSignInAgain()
    {
        // AC-11. Signing in again rewrites the same file in the same place, so the advice that
        // fixes a signed-out state does nothing here except waste the user's time and convince
        // them the app is broken.
        var message = Describe(new FetchOutcome.AuthFailed(AuthFailureKind.PermissionDenied));

        Assert.DoesNotContain("sign in", message.Advice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission", message.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void APermissionFailureIsNamedDifferentlyFromBeingSignedOut()
    {
        // AC-11 again, from the other side: the two states must be distinguishable at a glance
        // on the HUD, which shows the headline and nothing else.
        var denied = Describe(new FetchOutcome.AuthFailed(AuthFailureKind.PermissionDenied));
        var signedOut = Describe(new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut));

        Assert.NotEqual(signedOut.Headline, denied.Headline);
    }

    [Fact]
    public void AnUnexplainedAuthFailureDoesNotInventAReason()
    {
        // The response said nothing about why. Claiming the user is signed out, or that a file
        // is unreadable, would be a diagnosis on no evidence.
        var message = Describe(new FetchOutcome.AuthFailed(AuthFailureKind.Unspecified));

        Assert.Equal("Sign-in failed", message.Headline);
        Assert.DoesNotContain("permission", message.Advice, StringComparison.OrdinalIgnoreCase);
    }

    // ---- The response itself (AC-9) ----

    [Fact]
    public void AnUnreadableResponseRepeatsTheReasonItGave()
    {
        // The reason is the only clue anyone gets about an undocumented endpoint that changed.
        // Dropping it would leave a user, and a bug report, with nothing to go on.
        var message = Describe(new FetchOutcome.Unreadable("no window keys found"));

        Assert.Equal("Response unreadable", message.Headline);
        Assert.Contains("no window keys found", message.Advice);
    }

    [Fact]
    public void ATransientFailureTellsTheUserToDoNothing()
    {
        // A network that dropped for one poll is not the user's problem to solve, and advice
        // that invites them to act on it would be advice to act on nothing.
        var message = Describe(new FetchOutcome.Transient(null));

        Assert.Equal("Endpoint unavailable", message.Headline);
        Assert.Contains("retry", message.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnsupportedAccountSaysPollingHasStoppedAndNamesWhatItAnswered()
    {
        // Polling stops hard here, so the panel has to say so: a HUD that is not updating and
        // does not admit it is the failure principle 6 exists to prevent.
        var message = Describe(new FetchOutcome.Unsupported(403));

        Assert.Equal("Not available on this account", message.Headline);
        Assert.Contains("403", message.Advice);
        Assert.Contains("stopped", message.Advice, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Before the first reading ----

    [Fact]
    public void HavingNoReadingYetIsItsOwnStateRatherThanAnError()
    {
        // The state every launch passes through. Wording it as a failure would make the app look
        // broken for the first few seconds of every run.
        var message = Describe(hasReading: false);

        Assert.Equal("No reading yet", message.Headline);
    }

    // ---- The mark beside a number (AC-8, AC-13) ----

    [Fact]
    public void ACurrentReadingCarriesNoMark()
    {
        var message = StatusMessage.Describe(State(new FetchOutcome.Transient(null)));

        Assert.NotNull(message);
        Assert.Null(message.Mark);
    }

    [Fact]
    public void AStaleReadingIsMarkedWithItsAge()
    {
        // AC-8. The age is what separates a reading that is old and known to be old from one
        // presented as current.
        var message = Describe(
            new FetchOutcome.Transient(null),
            Freshness.Stale,
            TimeSpan.FromMinutes(48));

        Assert.Equal("48 min old", message.Mark);
    }

    [Fact]
    public void AFrozenReadingIsMarkedWithWhyItWillNotUpdate()
    {
        // AC-13. An age alone would suggest a newer reading is on its way. Nothing is coming
        // until the user does something, and the mark says which something.
        var message = Describe(
            new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
            Freshness.Frozen,
            TimeSpan.FromHours(2));

        Assert.Equal("frozen, signed out", message.Mark);
    }

    [Fact]
    public void TwoFrozenCausesAreMarkedDifferently()
    {
        // The mark is all the HUD shows. If every frozen reading said only "frozen", AC-11's
        // distinction would exist in the panel and nowhere a user would look first.
        var denied = Describe(
            new FetchOutcome.AuthFailed(AuthFailureKind.PermissionDenied),
            Freshness.Frozen);
        var signedOut = Describe(
            new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
            Freshness.Frozen);

        Assert.NotEqual(signedOut.Mark, denied.Mark);
    }

    // ---- The guard over the whole table ----

    [Theory]
    [MemberData(nameof(EveryFailure))]
    public void EveryStateSaysWhatToDoAboutIt(FetchOutcome failure)
    {
        // The half that is easy to leave out. A headline naming a state the user cannot act on
        // is a dead end, and this is the criterion's second clause held over the whole table.
        var message = Describe(failure);

        Assert.NotEmpty(message.Headline);
        Assert.NotEmpty(message.Advice);
        Assert.EndsWith(".", message.Advice);
    }

    public static TheoryData<FetchOutcome> EveryFailure() =>
    [
        new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
        new FetchOutcome.AuthFailed(AuthFailureKind.Invalidated),
        new FetchOutcome.AuthFailed(AuthFailureKind.PermissionDenied),
        new FetchOutcome.AuthFailed(AuthFailureKind.Unspecified),
        new FetchOutcome.Unreadable("a reason"),
        new FetchOutcome.Transient(null),
        new FetchOutcome.Unsupported(404),
    ];
}
