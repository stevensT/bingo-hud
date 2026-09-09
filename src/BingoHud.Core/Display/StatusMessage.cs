using BingoHud.Core.Monitoring;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// What Bingo says when it is not simply showing current numbers (AC-9, AC-10, AC-11).
///
/// <para>
/// Every state that is not a plain current reading says two things: what happened, and what to
/// do about it. The second half is the one that is easy to leave out, and AC-11 exists because
/// leaving it out has a specific cost — "sign-in failed" is true both of a missing credential
/// and of a credential Windows refuses to open, and a user told to sign in again when the real
/// problem is file permissions will do it, watch nothing change, and conclude the app is broken.
/// </para>
/// <para>
/// One table for all of it, so a state cannot be named one way on the HUD and another in the
/// panel. The words live in Core with every other string the user sees; the WPF layer places
/// them and composes none of them. <see cref="Describe"/> is the only way to obtain one, which is
/// what makes that guarantee hold rather than merely be intended.
/// </para>
/// </summary>
/// <param name="Headline">
/// The state in a few words, sized for the HUD, which shows this and nothing else when it has no
/// numbers to show.
/// </param>
/// <param name="Advice">
/// What to do about it, for the panel, which has room for a sentence. Always present: a headline
/// naming a state the user cannot act on is a dead end.
/// </param>
public sealed record StatusMessage(string Headline, string Advice)
{
    /// <summary>
    /// Said before the first poll finishes. Public because the shell needs the same words for
    /// the instant before Core exists to ask, and two copies of one phrase is how two surfaces
    /// end up disagreeing.
    /// </summary>
    public const string NoReadingYet = "No reading yet";

    /// <summary>
    /// The floor that keeps the HUD from ever being blank: said only if a state yields no lines
    /// and no words of its own, which nothing does today. It exists so that narrowing which
    /// windows get a line cannot quietly produce an empty display.
    /// </summary>
    public const string NoWindowToShow = "No window to show";

    /// <summary>
    /// The current state in words, or null when a current reading has nothing wrong with it.
    ///
    /// <para>
    /// A failure outranks staleness, because it is the more specific fact: a reading that is
    /// forty minutes old because the token expired should say the token expired rather than that
    /// it is forty minutes old. The age is not lost — <see cref="MarkFor"/> carries it to the
    /// place it belongs, beside the numbers, so both facts are on screen at once.
    /// </para>
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    public static StatusMessage? Describe(ReadingState state)
    {
        if (state.LastFailure is { } failure)
        {
            var (headline, advice) = Words(failure);

            return new StatusMessage(headline, advice);
        }

        if (state.Last is not { } snapshot)
        {
            return new StatusMessage(
                NoReadingYet,
                "Bingo has not finished a poll since it started.");
        }

        // A response can succeed and still name nothing the HUD draws: the normalizer accepts any
        // snapshot with at least one window, and a per-model cap is a window. Without this the
        // app would have a reading, no failure, nothing to draw and nothing to say, which is a
        // blank HUD — indistinguishable from a crashed one.
        if (!snapshot.Windows.Any(w => Readout.HudKinds.Contains(w.Kind)))
        {
            return new StatusMessage(
                "No 5h or weekly window reported",
                "The response carried only per-model caps, which the panel lists. Either this "
                + "account has no 5-hour or weekly limit, or the endpoint has renamed them.");
        }

        if (state.Freshness != Freshness.Fresh)
        {
            // Reachable when polls simply stopped happening — a machine that slept, most
            // obviously — rather than because an attempt failed. There is nothing to diagnose
            // and nothing to advise, so it says the one true thing and gives the age.
            return new StatusMessage(
                "Reading is stale",
                $"No poll has succeeded in {AgeText.Span(state.Age)}.");
        }

        return null;
    }

    /// <summary>
    /// What sits beside the numbers when they are on screen but not current, or null when they
    /// are current.
    ///
    /// <para>
    /// Asked for separately rather than carried as a field, because it answers a different
    /// question from the headline: not what state the app is in, but whether what is drawn can
    /// still be acted on. Only the caller that draws numbers has any use for it.
    /// </para>
    /// <para>
    /// Every mark leads with the age, so its shape does not change under the reader as time
    /// passes. A frozen reading adds the cause, because a frozen reading never becomes stale and
    /// would otherwise carry no hint that it is waiting on the user rather than on a poll.
    /// </para>
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    public static string? MarkFor(ReadingState state) => state.Freshness switch
    {
        Freshness.Stale => AgeText.Old(state.Age),

        Freshness.Frozen => state.LastFailure is { } failure
            ? $"{AgeText.Old(state.Age)}, {Lowered(Words(failure).Headline)}"
            // Unreachable as the monitor is written: it freezes a reading only on a failure that
            // cannot pass. Kept because a display that throws takes the window down with it.
            : AgeText.Old(state.Age),

        // A reading can be current and still have stopped being refreshed. A dropped connection
        // is expected to pass on its own and is deliberately left unmarked, but a response that
        // no longer parses will not fix itself, and a figure nothing is updating must not sit
        // there bare beside a countdown that keeps moving (AC-9).
        _ => state.LastFailure is FetchOutcome.Unreadable ? "last poll unreadable" : null,
    };

    /// <summary>
    /// The words for one failure: what happened, and what to do about it.
    /// </summary>
    private static (string Headline, string Advice) Words(FetchOutcome failure) => failure switch
    {
        FetchOutcome.AuthFailed { Kind: AuthFailureKind.SignedOut } => (
            "Signed out",
            "Bingo found no Claude Code credential on this machine. Sign in by running claude "
            + "in a terminal, and the next poll will pick it up."),

        FetchOutcome.AuthFailed { Kind: AuthFailureKind.Invalidated } => (
            "Sign-in expired",
            "The server rejected the saved token, which is an ordinary daily event. Sign in "
            + "again by running claude in a terminal."),

        // AC-11. The advice that fixes a signed-out state does nothing here: a fresh sign-in
        // writes the same file to the same place, and the operating system refuses it for the
        // same reason. Saying so is the point — otherwise the user does the useless thing.
        FetchOutcome.AuthFailed { Kind: AuthFailureKind.PermissionDenied } => (
            "Credential unreadable",
            "The credential file is there, but Windows will not let Bingo open it. Check its "
            + "permissions; a fresh sign-in would write the same file to the same place and "
            + "change nothing."),

        // No evidence either way, so no diagnosis. Naming a cause here would be a guess dressed
        // as a finding, and the user would act on it. Matched explicitly rather than as a
        // catch-all for AuthFailed, so that a kind added later reaches the default arm and fails
        // a test instead of quietly inheriting sign-in advice that may be wrong for it.
        FetchOutcome.AuthFailed { Kind: AuthFailureKind.Unspecified } => (
            "Sign-in failed",
            "Authentication failed and the response did not say why. Running claude in a "
            + "terminal to sign in again is the first thing to try."),

        // The reason is the only clue anyone gets about an undocumented endpoint that changed
        // shape. It goes on screen verbatim so that a bug report can carry it.
        FetchOutcome.Unreadable unreadable => (
            "Response unreadable",
            $"The endpoint answered, but Bingo found no usage window in it: {unreadable.Reason}. "
            + "This endpoint is undocumented, so a change upstream is the likely cause."),

        // Not the user's problem to solve, so the advice is explicitly to do nothing. Advice
        // that invites action on something no action reaches is worse than silence.
        FetchOutcome.Transient => (
            "Endpoint unavailable",
            "The last attempt did not get through. Bingo will retry on its own; nothing needs "
            + "doing."),

        // The one outcome that stops the loop for good, so the panel has to say so. A HUD that
        // has quietly given up is exactly what principle 6 exists to prevent.
        FetchOutcome.Unsupported unsupported => (
            "Not available on this account",
            $"The endpoint answered {unsupported.StatusCode} and will not serve this account. "
            + "Bingo has stopped polling and will not try again until it is restarted."),

        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };

    /// <summary>
    /// A headline recased to sit inside the mark, after the age.
    ///
    /// <para>
    /// Derived from the headline rather than kept as a second table, so the two can never drift
    /// into naming one state two ways. The assumption this rests on is that no headline begins
    /// with a proper noun — true of all of them above, and the reason to check here before
    /// adding one that does.
    /// </para>
    /// </summary>
    private static string Lowered(string headline) =>
        char.ToLowerInvariant(headline[0]) + headline[1..];
}
