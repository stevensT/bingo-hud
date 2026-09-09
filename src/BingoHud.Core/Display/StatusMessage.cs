using BingoHud.Core.Monitoring;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// What Bingo says when it is not simply showing two current numbers (AC-9, AC-10, AC-11).
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
/// them and composes none of them.
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
/// <param name="Mark">
/// What goes beside a number that is still on screen but is not current, or null when the
/// reading is current. This is what stops a stale or frozen figure being read as live (AC-8,
/// AC-13).
/// </param>
public sealed record StatusMessage(string Headline, string Advice, string? Mark)
{
    /// <summary>
    /// The current state in words, or null when a current reading has nothing wrong with it.
    ///
    /// <para>
    /// A failure outranks staleness, because it is the more specific fact: a reading that is
    /// forty minutes old because the token expired should say the token expired, not that it is
    /// forty minutes old. The age is not lost — it goes to <see cref="Mark"/>, which sits beside
    /// the numbers, so both facts are on screen at once.
    /// </para>
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    public static StatusMessage? Describe(ReadingState state)
    {
        var mark = MarkFor(state);

        if (state.LastFailure is { } failure)
        {
            var (headline, advice) = Words(failure);

            return new StatusMessage(headline, advice, mark);
        }

        if (state.Last is null)
        {
            return new StatusMessage(
                "No reading yet",
                "Bingo has not finished a poll since it started.",
                mark);
        }

        if (state.Freshness != Freshness.Fresh)
        {
            // Reachable when polls simply stopped happening — a machine that slept, most
            // obviously — rather than because an attempt failed. There is nothing to diagnose
            // and nothing to advise, so it says the one true thing and gives the age.
            return new StatusMessage(
                "Reading is stale",
                $"No poll has succeeded in {AgeText.Span(state.Age)}.",
                mark);
        }

        return null;
    }

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
        // as a finding, and the user would act on it.
        FetchOutcome.AuthFailed => (
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
    /// What sits beside a number that is on screen but not current, or null when it is current.
    ///
    /// <para>
    /// A stale reading is marked with its age, because a poll was merely missed and a newer one
    /// is coming. A frozen reading is marked with the reason instead: an age alone would suggest
    /// a refresh is on its way, when in fact nothing will change until the user does something,
    /// and the mark is the only place the HUD can say which something.
    /// </para>
    /// </summary>
    private static string? MarkFor(ReadingState state) => state.Freshness switch
    {
        Freshness.Stale => AgeText.Old(state.Age),
        Freshness.Frozen => state.LastFailure is { } failure
            ? $"frozen, {Lowered(Words(failure).Headline)}"
            // Unreachable as the monitor is written: it freezes a reading only on a failure that
            // cannot pass. Kept because a display that throws takes the window down with it.
            : "frozen",
        _ => null,
    };

    /// <summary>
    /// A headline recased to sit inside the mark, after the word "frozen".
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
