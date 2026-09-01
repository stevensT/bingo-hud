using System.Globalization;

namespace BingoHud.Core.Display;

/// <summary>
/// Renders a window's reset time as the phrase shown beside its percentage.
///
/// <para>
/// Presentation logic, and it lives in Core on purpose. "Are these the right words at this
/// moment" is answered by a test in milliseconds and by staring at a HUD for an hour otherwise.
/// The WPF layer places the string; it does not decide it.
/// </para>
/// <para>
/// Absolute when the reset is distant, relative as it nears. Both halves earn their place:
/// "resets in 53 min" says nothing useful about something five days away, and "resets 1:00 AM"
/// says nothing about whether there is time to finish the current task.
/// </para>
/// </summary>
public static class ResetFormatter
{
    /// <summary>
    /// Inside this, the countdown is what matters. Outside it, the wall-clock time is.
    /// </summary>
    private static readonly TimeSpan RelativeWithin = TimeSpan.FromMinutes(60);

    /// <summary>
    /// The reset phrase, or null when there is nothing to say.
    /// </summary>
    /// <param name="resetsAt">
    /// When the window resets, or null. Null is an observed, ordinary case, and the right output
    /// for it is nothing at all — an invented countdown would be a figure with nothing behind
    /// it.
    /// </param>
    /// <param name="now">The current instant, carrying the offset the phrase is rendered in.</param>
    /// <param name="culture">
    /// Whose clock conventions to use. Twelve- or twenty-four-hour is a Windows setting, not a
    /// choice for Bingo to make.
    /// </param>
    public static string? Describe(
        DateTimeOffset? resetsAt,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        if (resetsAt is not { } reset)
        {
            return null;
        }

        culture ??= CultureInfo.CurrentCulture;

        var remaining = reset - now;

        if (remaining <= TimeSpan.Zero)
        {
            // The server's reset time can sit slightly behind the actual reset. Counting
            // backwards would be nonsense, and announcing that it has reset would be a claim
            // about a reading Bingo has not taken yet.
            return "resets any moment";
        }

        if (remaining < TimeSpan.FromMinutes(1))
        {
            // "resets in 0 min" reads as though it has already happened.
            return "resets in under a minute";
        }

        if (remaining < RelativeWithin)
        {
            var minutes = (int)remaining.TotalMinutes;

            return $"resets in {minutes} min";
        }

        // Rendered in the caller's offset: the endpoint reports UTC, and the whole value of the
        // line is being readable against the user's own clock.
        var local = reset.ToOffset(now.Offset);
        var time = local.ToString(culture.DateTimeFormat.ShortTimePattern, culture);

        if (local.Date == now.Date)
        {
            return $"resets {time}";
        }

        // Any other day carries its name. A bare time of day for something days away reads as
        // tonight, which is the one way this line can actively mislead.
        var day = culture.DateTimeFormat.AbbreviatedDayNames[(int)local.DayOfWeek];

        return $"resets {day} {time}";
    }
}
