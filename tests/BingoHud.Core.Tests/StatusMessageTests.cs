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
/// The mark is separate, and deliberately not a field on the message: it answers a different
/// question — not what state the app is in, but whether the numbers currently on screen can still
/// be acted on. It is asked for by name, by the one caller that draws numbers.
/// </para>
/// </summary>
public class StatusMessageTests
{
    private static QuotaWindow Window(WindowKind kind, double used = 83) =>
        new(kind, used, null, ServerSeverity.Normal, kind == WindowKind.WeeklyScoped ? "a-model" : null);

    private static QuotaSnapshot Snapshot(params WindowKind[] kinds) =>
        new(
            (kinds.Length == 0 ? [WindowKind.Session] : kinds).Select(k => Window(k)).ToList(),
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.FromHours(-7)),
            RawBody: "{}");

    private static ReadingState State(
        FetchOutcome? failure = null,
        Freshness freshness = Freshness.Fresh,
        TimeSpan? age = null,
        bool hasReading = true,
        QuotaSnapshot? snapshot = null) =>
        new(
            hasReading ? snapshot ?? Snapshot() : null,
            freshness,
            failure,
            age ?? TimeSpan.Zero,
            "test");

    private static StatusMessage Describe(
        FetchOutcome? failure = null,
        Freshness freshness = Freshness.Fresh,
        TimeSpan? age = null,
        bool hasReading = true,
        QuotaSnapshot? snapshot = null)
    {
        var message = StatusMessage.Describe(State(failure, freshness, age, hasReading, snapshot));

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

    // ---- A reading that arrived but named nothing the HUD shows ----

    [Fact]
    public void AReadingCarryingNoWindowTheHudShowsIsItsOwnState()
    {
        // A successful response can carry only per-model caps: the normalizer accepts any
        // snapshot with at least one window, and the HUD draws only the session and weekly-all
        // windows. Without this arm the app has a reading, no failure, nothing to draw and
        // nothing to say — which is a blank HUD, the one outcome principle 6 forbids outright.
        var message = Describe(snapshot: Snapshot(WindowKind.WeeklyScoped));

        Assert.Equal("No 5h or weekly window reported", message.Headline);
        Assert.Contains("per-model", message.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AReadingCarryingOneWindowTheHudShowsIsNotThatState()
    {
        // One of the two is enough to draw. Only the absence of both is the empty state.
        Assert.Null(StatusMessage.Describe(State(snapshot: Snapshot(WindowKind.WeeklyAll))));
    }

    // ---- Before the first reading ----

    [Fact]
    public void HavingNoReadingYetIsItsOwnStateRatherThanAnError()
    {
        // The state every launch passes through. Wording it as a failure would make the app look
        // broken for the first few seconds of every run.
        var message = Describe(hasReading: false);

        Assert.Equal(StatusMessage.NoReadingYet, message.Headline);
    }

    // ---- A machine that slept: stale with nothing having failed ----

    [Fact]
    public void AStaleReadingWithNoFailureSaysHowLongSinceAPollSucceeded()
    {
        // Reachable when polls simply stopped happening rather than failing. There is nothing to
        // diagnose, so the honest thing is to say how far behind the numbers are.
        var message = Describe(freshness: Freshness.Stale, age: TimeSpan.FromMinutes(48));

        Assert.Equal("Reading is stale", message.Headline);
        Assert.Equal("No poll has succeeded in 48 min.", message.Advice);
    }

    [Fact]
    public void TheStaleAdviceReadsAsADurationRatherThanAnAge()
    {
        // "No poll has succeeded in 2 hours old" is what using the label phrasing here would
        // produce. The two phrasings exist to keep this sentence grammatical.
        var message = Describe(freshness: Freshness.Stale, age: TimeSpan.FromHours(2));

        Assert.Equal("No poll has succeeded in 2 hours.", message.Advice);
    }

    // ---- A failure outranks staleness ----

    [Fact]
    public void AStaleReadingWhoseLastPollFailedReportsTheFailureNotTheStaleness()
    {
        // Both facts are true and the failure is the more specific one: it says what to do.
        // Reporting staleness here would drop AC-11's advice on the floor at the moment the
        // user most needs it.
        var message = Describe(
            new FetchOutcome.AuthFailed(AuthFailureKind.PermissionDenied),
            Freshness.Stale,
            TimeSpan.FromMinutes(48));

        Assert.Equal("Credential unreadable", message.Headline);
    }

    [Fact]
    public void TheAgeSurvivesAFailureBecauseItTravelsInTheMark()
    {
        // The other half of the rule above: the failure takes the headline, and the age is not
        // lost because the mark carries it. Both facts reach the screen at once.
        var state = State(
            new FetchOutcome.Transient(null),
            Freshness.Stale,
            TimeSpan.FromMinutes(48));

        Assert.Equal("48 min old", StatusMessage.MarkFor(state));
    }

    // ---- The mark beside a number (AC-8, AC-13) ----

    [Fact]
    public void ACurrentReadingCarriesNoMark()
    {
        Assert.Null(StatusMessage.MarkFor(State()));
    }

    [Fact]
    public void APassingBlipLeavesACurrentReadingUnmarked()
    {
        // One 503 after a good poll. The number is still current and the next attempt is already
        // scheduled; marking it would blank the meaning out of the mark on every network blip.
        Assert.Null(StatusMessage.MarkFor(State(new FetchOutcome.Transient(null))));
    }

    [Fact]
    public void ACurrentReadingWhoseResponseStoppedParsingIsMarked()
    {
        // AC-9. An unreadable response does not freeze the reading, because the endpoint may
        // start making sense again — but it will not do so on its own the way a dropped
        // connection will, so a number that nothing is refreshing must not sit there bare.
        Assert.Equal(
            "last poll unreadable",
            StatusMessage.MarkFor(State(new FetchOutcome.Unreadable("shape changed"))));
    }

    [Fact]
    public void AStaleReadingIsMarkedWithItsAge()
    {
        // AC-8. The age is what separates a reading that is old and known to be old from one
        // presented as current.
        Assert.Equal(
            "48 min old",
            StatusMessage.MarkFor(State(freshness: Freshness.Stale, age: TimeSpan.FromMinutes(48))));
    }

    [Fact]
    public void AFrozenReadingIsMarkedWithBothItsAgeAndWhyItWillNotUpdate()
    {
        // AC-13 and principle 6's age clause together. The cause alone would leave a week-old
        // number looking as recent as a minute-old one, because a frozen reading never becomes
        // stale and so never picks up an age from anywhere else.
        Assert.Equal(
            "6 days old, signed out",
            StatusMessage.MarkFor(State(
                new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
                Freshness.Frozen,
                TimeSpan.FromDays(6))));
    }

    [Fact]
    public void AFreshlyFrozenReadingStillLeadsWithItsAge()
    {
        // The age leads in every mark, so the shape does not change under the reader as time
        // passes. "just now" is the honest reading a second after the token expired.
        Assert.Equal(
            "just now, signed out",
            StatusMessage.MarkFor(State(
                new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
                Freshness.Frozen,
                TimeSpan.FromSeconds(20))));
    }

    [Fact]
    public void AnUnsupportedAccountIsAlsoMarkedWithItsCause()
    {
        // The other of the two ways to freeze, and the one where the mark matters most: nothing
        // will ever replace this number, because polling has stopped for good.
        Assert.Equal(
            "3 hours old, not available on this account",
            StatusMessage.MarkFor(State(
                new FetchOutcome.Unsupported(403),
                Freshness.Frozen,
                TimeSpan.FromHours(3))));
    }

    [Fact]
    public void TwoFrozenCausesAreMarkedDifferently()
    {
        // The mark is all the HUD says about the state. If every frozen reading said only that
        // it was frozen, AC-11's distinction would exist in the panel and nowhere a user would
        // look first.
        var frozen = (AuthFailureKind kind) => StatusMessage.MarkFor(State(
            new FetchOutcome.AuthFailed(kind),
            Freshness.Frozen,
            TimeSpan.FromHours(1)));

        Assert.NotEqual(frozen(AuthFailureKind.SignedOut), frozen(AuthFailureKind.PermissionDenied));
    }

    // ---- Fences over the table itself ----

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

    [Fact]
    public void EveryFailureOutcomeHasARowInTheTable()
    {
        // The table is written by hand, so nothing but this stops a new FetchOutcome shipping
        // with no words. Without it the first symptom would be the app throwing once a second
        // from the render timer, because an unmapped outcome hits the default arm.
        var declared = typeof(FetchOutcome).GetNestedTypes()
            .Where(t => t.IsSealed && t.IsSubclassOf(typeof(FetchOutcome)))
            .Select(t => t.Name)
            .Where(name => name != nameof(FetchOutcome.Success))
            .ToHashSet();

        var covered = AllFailures.Select(f => f.GetType().Name).ToHashSet();

        Assert.Equal(declared, covered);
    }

    [Theory]
    [MemberData(nameof(EveryAuthFailureKind))]
    public void EveryAuthFailureKindIsNamedRatherThanQuietlyTakingSignInAdvice(AuthFailureKind kind)
    {
        // AC-11's real hazard is confident wrong advice, so a new kind must not silently inherit
        // "sign in again". Each kind is matched explicitly and this fails the day one is added.
        var message = Describe(new FetchOutcome.AuthFailed(kind));

        Assert.NotEmpty(message.Headline);
        Assert.NotEmpty(message.Advice);
    }

    private static readonly IReadOnlyList<FetchOutcome> AllFailures =
    [
        new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
        new FetchOutcome.Unreadable("a reason"),
        new FetchOutcome.Transient(null),
        new FetchOutcome.Unsupported(404),
    ];

    public static TheoryData<FetchOutcome> EveryFailure()
    {
        var data = new TheoryData<FetchOutcome>();

        foreach (var failure in AllFailures)
        {
            data.Add(failure);
        }

        foreach (var kind in Enum.GetValues<AuthFailureKind>())
        {
            data.Add(new FetchOutcome.AuthFailed(kind));
        }

        return data;
    }

    public static TheoryData<AuthFailureKind> EveryAuthFailureKind()
    {
        var data = new TheoryData<AuthFailureKind>();

        foreach (var kind in Enum.GetValues<AuthFailureKind>())
        {
            data.Add(kind);
        }

        return data;
    }
}
