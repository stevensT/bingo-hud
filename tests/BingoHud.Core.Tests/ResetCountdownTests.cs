using System.Globalization;
using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// The short countdown shown in place of the reset phrase when the user picks it (display
/// options AC-2, AC-4).
///
/// <para>
/// One unit, the largest that is at least one whole, with days and hours to a tenth and minutes
/// whole. Always rounded down: a countdown that rounds up claims more time than there is, and
/// "4.9d" at 4d 23h is the honest reading of a clock that has not reached five.
/// </para>
/// <para>
/// Each unit boundary is tested on both sides, because a boundary is where an off-by-one hides.
/// </para>
/// </summary>
public class ResetCountdownTests
{
    private static readonly CultureInfo English = new("en-US");
    private static readonly CultureInfo German = new("de-DE");

    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static string? Countdown(TimeSpan untilReset, CultureInfo? culture = null) =>
        ResetFormatter.Countdown(Now + untilReset, Now, culture ?? English);

    [Fact]
    public void AWindowWithNoResetTimeHasNoCountdown()
    {
        Assert.Null(ResetFormatter.Countdown(null, Now, English));
    }

    [Theory]
    [InlineData(4, 12, 0, "4.5d")]
    [InlineData(1, 0, 0, "1.0d")]          // exactly a day: days take over
    [InlineData(4, 23, 0, "4.9d")]         // never rounded up to 5.0
    [InlineData(6, 23, 59, "6.9d")]
    [InlineData(7, 0, 0, "7.0d")]          // the longest the weekly window can be
    public void ADayOrMoreIsCountedInDaysToATenth(int days, int hours, int minutes, string expected)
    {
        Assert.Equal(expected, Countdown(new TimeSpan(days, hours, minutes, 0)));
    }

    [Theory]
    [InlineData(23, 59, "23.9h")]          // a minute short of a day stays in hours
    [InlineData(1, 0, "1.0h")]             // exactly an hour: hours take over
    [InlineData(2, 18, "2.3h")]            // exactly on a tenth: must not floor to 2.2
    [InlineData(2, 17, "2.2h")]            // a minute short of the next tenth
    [InlineData(4, 59, "4.9h")]
    public void AnHourOrMoreIsCountedInHoursToATenth(int hours, int minutes, string expected)
    {
        Assert.Equal(expected, Countdown(new TimeSpan(hours, minutes, 0)));
    }

    [Theory]
    [InlineData(59, 59, "59m")]            // a second short of an hour stays in minutes
    [InlineData(53, 30, "53m")]            // rounded down
    [InlineData(1, 0, "1m")]
    public void InsideTheLastHourItIsWholeMinutes(int minutes, int seconds, string expected)
    {
        Assert.Equal(expected, Countdown(new TimeSpan(0, minutes, seconds)));
    }

    [Theory]
    [InlineData(59)]
    [InlineData(1)]
    public void UnderAMinuteIsNotZero(int seconds)
    {
        // "0m" reads as already reset, which is a claim about a reading not yet taken.
        Assert.Equal("<1m", Countdown(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-90)]
    public void AResetThatIsDueOrPastIsStillUnderAMinute(int seconds)
    {
        // The server's reset time can sit slightly behind the real reset. Counting backwards
        // would be nonsense, and the next poll will bring the new window.
        Assert.Equal("<1m", Countdown(TimeSpan.FromSeconds(seconds)));
    }

    public static TheoryData<long, string> EveryTenthBoundary()
    {
        var data = new TheoryData<long, string>();

        foreach (var (unit, suffix, first, last) in new[]
        {
            (TimeSpan.TicksPerHour, "h", 10, 239),
            (TimeSpan.TicksPerDay, "d", 10, 70),
        })
        {
            for (var tenths = first; tenths <= last; tenths++)
            {
                var exact = tenths * unit / 10;
                data.Add(exact, $"{tenths / 10}.{tenths % 10}{suffix}");

                // One tick short of the boundary still reads the tenth below it, except where
                // that crosses from days back into hours, which the boundary tests above cover.
                if (tenths > first)
                {
                    data.Add(exact - 1, $"{(tenths - 1) / 10}.{(tenths - 1) % 10}{suffix}");
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTenthBoundary))]
    public void EveryTenthBoundaryFloorsToItsOwnTenth(long ticks, string expected)
    {
        // Integer arithmetic on ticks is the reference. Flooring a double can drop a tenth on an
        // exact boundary under a different formula for the same quantity; this pins that the one
        // in use does not, at every boundary the HUD can show.
        Assert.Equal(expected, Countdown(TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void TheDecimalMarkFollowsTheUsersLocale()
    {
        Assert.Equal("4,5d", Countdown(new TimeSpan(4, 12, 0, 0), German));
        Assert.Equal("2,3h", Countdown(new TimeSpan(2, 18, 0), German));
    }
}
