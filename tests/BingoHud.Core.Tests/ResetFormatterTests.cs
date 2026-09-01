using System.Globalization;
using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// The reset phrase shown beside each percentage (AC-3).
///
/// <para>
/// It lives in Core rather than in the WPF layer, deliberately. "Is this the right words for the
/// right moment" is the kind of question that is answered by a test in milliseconds and by
/// staring at a HUD for an hour otherwise.
/// </para>
/// <para>
/// Absolute when distant, relative as it nears. Both halves matter: "resets in 53 min" is
/// useless for something five days away, and "resets 1:00 AM" tells someone nothing about
/// whether they can finish what they are doing.
/// </para>
/// </summary>
public class ResetFormatterTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");
    private static readonly CultureInfo TwentyFourHour = new("de-DE");

    /// <summary>
    /// A Monday, deliberately mid-morning so that "later today" and "tomorrow" are both
    /// reachable without crossing a month.
    /// </summary>
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static string? Describe(DateTimeOffset? resetsAt, CultureInfo? culture = null) =>
        ResetFormatter.Describe(resetsAt, Now, culture ?? TwelveHour);

    [Fact]
    public void AWindowWithNoResetTimeSaysNothingAtAll()
    {
        // Observed in the live payload. Nothing is the correct output: an invented countdown
        // would be a number with nothing behind it.
        Assert.Null(Describe(null));
    }

    [Fact]
    public void AResetWithinTheHourIsRelative()
    {
        Assert.Equal("resets in 53 min", Describe(Now.AddMinutes(53)));
    }

    [Fact]
    public void ASingleMinuteIsNotPluralised()
    {
        Assert.Equal("resets in 1 min", Describe(Now.AddMinutes(1)));
    }

    [Fact]
    public void LessThanAMinuteIsNotRoundedToZero()
    {
        // "resets in 0 min" reads as though it has already happened.
        Assert.Equal("resets in under a minute", Describe(Now.AddSeconds(20)));
    }

    [Fact]
    public void AResetThatHasPassedSaysSoRatherThanCountingBackwards()
    {
        // The server's reset time can be a little behind the actual reset. A negative countdown
        // would be nonsense, and claiming it has reset would be a number Bingo has not seen.
        Assert.Equal("resets any moment", Describe(Now.AddMinutes(-3)));
    }

    [Fact]
    public void TheHourBoundaryIsWhereAbsoluteTakesOver()
    {
        Assert.Equal("resets in 59 min", Describe(Now.AddMinutes(59)));
        Assert.Equal("resets 10:34 AM", Describe(Now.AddMinutes(60)));
    }

    [Fact]
    public void ADistantResetLaterTodayIsATimeOfDay()
    {
        Assert.Equal("resets 9:30 PM", Describe(Now.AddHours(11).AddMinutes(56)));
    }

    [Fact]
    public void AResetOnAnotherDayCarriesTheDayOfWeek()
    {
        // The weekly window is usually days out. A bare time of day for something five days
        // away is actively misleading — it reads as tonight.
        Assert.Equal("resets Sat 1:00 AM", Describe(Now.AddDays(4).AddHours(15).AddMinutes(26)));
    }

    [Fact]
    public void AResetTomorrowCarriesTheDayOfWeekToo()
    {
        Assert.Equal("resets Tue 8:00 AM", Describe(Now.AddDays(1).AddMinutes(-94)));
    }

    [Fact]
    public void AResetAFullWeekOutStillUsesTheDayName()
    {
        // A day name repeats every seven days, which would be an argument for adding a date
        // form. It is not worth it here: the weekly window is at most seven days, so there is
        // only ever one Monday within reach and the name cannot actually be misread.
        Assert.Equal("resets Mon 9:34 AM", Describe(Now.AddDays(7)));
    }

    [Fact]
    public void TheTimeIsRenderedInTheUsersOwnClockConvention()
    {
        // 12-hour and 24-hour is a Windows setting, not a choice for Bingo to make.
        Assert.Equal("resets 21:30", Describe(Now.AddHours(11).AddMinutes(56), TwentyFourHour));
    }

    [Fact]
    public void TheTimeIsShownInTheOffsetTheCallerIsIn()
    {
        // The endpoint reports UTC. 21:30 UTC is 2:30 PM in the caller's -07:00 offset, and the
        // whole point of the line is to be readable against the user's own clock.
        var resetsAtUtc = new DateTimeOffset(2026, 8, 31, 21, 30, 0, TimeSpan.Zero);

        Assert.Equal("resets 2:30 PM", Describe(resetsAtUtc));
    }

    [Fact]
    public void AResetExpressedInAnotherOffsetStillLandsOnTheRightDay()
    {
        // Same instant, written with a different offset. The phrase must not change.
        var utc = new DateTimeOffset(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
        var elsewhere = utc.ToOffset(TimeSpan.FromHours(5));

        Assert.Equal(Describe(utc), Describe(elsewhere));
    }
}
