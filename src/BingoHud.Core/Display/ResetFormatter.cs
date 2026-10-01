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
    /// An instant written out in full, for the detail panel.
    ///
    /// <para>
    /// The HUD abbreviates because it has one line and switches to a countdown as a reset nears.
    /// The panel does neither. It is the screen a user opens to check a number against their own
    /// clock, and a countdown cannot be checked against anything; nor can a bare time of day be
    /// told apart from the same time five days out.
    /// </para>
    /// </summary>
    /// <param name="instant">The moment to write out.</param>
    /// <param name="now">The current instant, carrying the offset the phrase is rendered in.</param>
    /// <param name="culture">Whose clock conventions to use.</param>
    public static string Exact(
        DateTimeOffset instant,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        var local = instant.ToOffset(now.Offset);
        var day = culture.DateTimeFormat.AbbreviatedDayNames[(int)local.DayOfWeek];
        var month = culture.DateTimeFormat.AbbreviatedMonthNames[local.Month - 1];
        var time = local.ToString(culture.DateTimeFormat.ShortTimePattern, culture);

        // Day name, day number, month, year, time — in that order regardless of culture, because
        // the ambiguity this line exists to remove is 5/9 against 9/5, and only a named month
        // removes it. The names and the clock format still come from the user's culture.
        return $"{day} {local.Day} {month} {local.Year}, {time}";
    }

    /// <summary>
    /// The reset as a short countdown, for the user who chose one over the clock-time phrase.
    ///
    /// <para>
    /// One unit, the largest that is at least one whole: "4.5d", "2.3h", "53m". Days and hours
    /// carry a tenth, always shown so the line does not change width as the figure crosses a whole
    /// number. Everything rounds down, because a countdown that rounds up claims time there is not.
    /// </para>
    /// <para>
    /// Flooring a double here is exact. Every tenth-of-an-hour and tenth-of-a-day boundary up to
    /// seven days was checked against integer arithmetic on ticks and none disagree, so the
    /// plainer form stands.
    /// </para>
    /// </summary>
    /// <param name="resetsAt">When the window resets, or null; null gives null, never a guess.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="culture">Whose decimal mark to use.</param>
    public static string? Countdown(DateTimeOffset? resetsAt, DateTimeOffset now, CultureInfo? culture = null)
    {
        if (resetsAt is not { } reset)
        {
            return null;
        }

        culture ??= CultureInfo.CurrentCulture;
        var remaining = reset - now;

        if (remaining.TotalDays >= 1)
        {
            return (Math.Floor(remaining.TotalDays * 10) / 10).ToString("0.0", culture) + "d";
        }

        if (remaining.TotalHours >= 1)
        {
            return (Math.Floor(remaining.TotalHours * 10) / 10).ToString("0.0", culture) + "h";
        }

        // Under a minute, and at or past the reset, both read "<1m". "0m" would say it has
        // already reset, which is a claim about a reading Bingo has not taken yet.
        return remaining.TotalMinutes >= 1 ? $"{(int)remaining.TotalMinutes}m" : "<1m";
    }

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
