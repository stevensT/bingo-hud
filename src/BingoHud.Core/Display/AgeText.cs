namespace BingoHud.Core.Display;

/// <summary>
/// How old a reading is, in the coarsest unit that is still true.
///
/// <para>
/// One vocabulary for the whole app, because both the panel and the HUD state a reading age.
/// Leaving each surface to phrase its own is how a panel saying "21 min old" ends up beside a HUD
/// saying "21m", which reads as two readings rather than one.
/// </para>
/// <para>
/// Coarse on purpose. The exact second a reading arrived is in the panel's last-poll line; what
/// this answers is the question actually being asked, which is whether the number can still be
/// acted on.
/// </para>
/// </summary>
public static class AgeText
{
    /// <summary>
    /// The age as a label, to sit beside a number: "just now", "21 min old", "6 days old".
    /// </summary>
    public static string Old(TimeSpan age) =>
        age < TimeSpan.FromMinutes(1) ? "just now" : $"{Span(age)} old";

    /// <summary>
    /// The age as a bare duration, to sit inside a sentence: "21 min", "3 hours", "6 days".
    ///
    /// <para>
    /// Separate from <see cref="Old"/> because under a minute the two want different words. A
    /// label wants "just now", which is a moment; a sentence such as "no poll has succeeded in
    /// ..." wants a length of time, and "in just now" is not English.
    /// </para>
    /// </summary>
    public static string Span(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "under a minute";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min";
        }

        if (age < TimeSpan.FromDays(1))
        {
            var hours = (int)age.TotalHours;

            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        // Days matter because a frozen reading never becomes stale and never refreshes: it sits
        // there for as long as the user leaves the cause unfixed. "144 hours old" is true and
        // useless.
        var days = (int)age.TotalDays;

        return days == 1 ? "1 day" : $"{days} days";
    }
}
