using BingoHud.Core.Monitoring;

namespace BingoHud.Core.Display;

/// <summary>
/// What the detail panel says after the user asks for a refresh (AC-28).
///
/// <para>
/// A refusal is a normal outcome rather than an error. A manual refresh is held to the same
/// floor as an automatic poll, so asking twice in a minute is refused by design, and the panel
/// says so. The alternative — a button that appears to do nothing — reads as broken, and a
/// button that claims to have refreshed when it did not would be the display lying about the age
/// of what is on screen.
/// </para>
/// <para>
/// Both halves of the criterion are here: why, and when the next attempt is possible. "Try again
/// later" would satisfy neither.
/// </para>
/// </summary>
public static class RefreshNotice
{
    /// <summary>
    /// The line to show, or null when there is nothing to say.
    /// </summary>
    /// <param name="result">
    /// What came of the last refresh the user asked for, or null if they have not asked. A
    /// success says nothing: the reading's age resets to "just now" and the numbers move if they
    /// moved, which is the feedback already.
    /// </param>
    /// <param name="now">The current instant, which the countdown is measured from.</param>
    public static string? Describe(RefreshResult? result, DateTimeOffset now)
    {
        if (result is not RefreshResult.Refused refused)
        {
            return null;
        }

        return $"Not yet. Next attempt {When(refused.NextAttemptAt - now)}, because {refused.Reason}.";
    }

    /// <summary>
    /// How far off the next attempt is.
    ///
    /// <para>
    /// Relative rather than a clock time. A refusal is never more than the ceiling away, and
    /// over that distance "in 3 min" answers the question a user actually has — whether to wait
    /// or go and do something else — in a way that "at 2:33 PM" does not.
    /// </para>
    /// </summary>
    private static string When(TimeSpan until)
    {
        if (until <= TimeSpan.Zero)
        {
            // The notice is composed at one instant and read at the next. A clock that crossed
            // the boundary in between must not produce a countdown running backwards.
            return "any moment";
        }

        return until < TimeSpan.FromMinutes(1)
            // "in 0 min" reads as though it has already happened.
            ? "in under a minute"
            : $"in {(int)until.TotalMinutes} min";
    }
}
