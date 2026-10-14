using System.Globalization;
using BingoHud.Core.Alerts;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Reset times read in the offset that applies when the reset happens, not the one that
/// applies now.
///
/// <para>
/// A weekly window can reset on the far side of a daylight-saving change. Rendered in today's
/// offset, a Monday reset seen from the Friday before the clocks go back reads an hour late —
/// the one kind of wrong a user checking it against their own clock on the day would notice.
/// </para>
/// <para>
/// The zone is passed in rather than read from the machine, so these hold anywhere. Without a
/// zone the formatter keeps the caller's offset, which keeps every other display test the same
/// on any machine; the shell passes the machine's zone, and <see cref="ShellTimeZoneTests"/>
/// holds it to that.
/// </para>
/// </summary>
public class ResetTimeZoneTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");

    /// <summary>
    /// US Eastern, built here rather than looked up. A lookup depends on the machine's zone data
    /// and globalization mode, and a failure in a static field would take the whole class down
    /// with an error that names neither.
    /// </summary>
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.CreateCustomTimeZone(
        "Test Eastern",
        TimeSpan.FromHours(-5),
        "Test Eastern",
        "Test Eastern Standard",
        "Test Eastern Daylight",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date,
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                // Second Sunday in March, 02:00, to first Sunday in November, 02:00.
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)),
        ]);

    /// <summary>Friday 30 October 2026, noon, daylight time. The clocks go back on Sunday.</summary>
    private static readonly DateTimeOffset Friday = new(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(-4));

    /// <summary>Monday 2 November, 10:00 standard time (UTC−05:00), not 11:00.</summary>
    private static readonly DateTimeOffset MondayReset = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheHudPhraseUsesTheOffsetAtTheReset()
    {
        Assert.Equal(
            "resets Mon 10:00 AM",
            ResetFormatter.Describe(MondayReset, Friday, TwelveHour, Eastern));
    }

    [Fact]
    public void ThePanelTimeUsesTheOffsetAtTheReset()
    {
        Assert.Equal(
            "Mon 2 Nov 2026, 10:00 AM",
            ResetFormatter.Exact(MondayReset, Friday, TwelveHour, Eastern));
    }

    [Fact]
    public void AResetJustAfterTheClocksChangeUsesTheNewOffset()
    {
        // 01:30 daylight time, and a reset at 04:00 standard time the same morning. In today's
        // offset it would read 5:00 AM.
        var beforeTheChange = new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4));
        var later = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal("resets 4:00 AM", ResetFormatter.Describe(later, beforeTheChange, TwelveHour, Eastern));
    }

    [Fact]
    public void TodayIsDecidedInTheZoneRatherThanInTheCallersOffset()
    {
        // Late on Thursday evening in New York, already Friday in UTC. A reset later that same
        // evening is today, and needs no day name. Judged by the UTC date it would read as
        // another day.
        var thursdayEveningInUtc = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        var laterThatEvening = new DateTimeOffset(2026, 10, 30, 3, 30, 0, TimeSpan.Zero);

        Assert.Equal(
            "resets 11:30 PM",
            ResetFormatter.Describe(laterThatEvening, thursdayEveningInUtc, TwelveHour, Eastern));
    }

    [Fact]
    public void WithoutAZoneTheCallersOffsetIsKept()
    {
        Assert.Equal("resets Mon 11:00 AM", ResetFormatter.Describe(MondayReset, Friday, TwelveHour));
    }

    [Fact]
    public void TheCountdownIsElapsedTimeWhateverTheClocksDo()
    {
        // Friday noon EDT to Monday 10:00 EST is 2 days and 23 hours of real time: the hour the
        // clocks give back is still an hour that passes.
        Assert.Equal("2.9d", ResetFormatter.Countdown(MondayReset, Friday, TwelveHour));
    }

    // ---- The zone reaches every screen that shows a reset ----

    private static ReadingState Reading() => new(
        new QuotaSnapshot(
            [new QuotaWindow(WindowKind.WeeklyAll, 40, MondayReset, ServerSeverity.Normal)],
            Friday,
            RawBody: "{}"),
        Freshness.Fresh,
        LastFailure: null,
        TimeSpan.Zero,
        "nothing is happening");

    [Fact]
    public void TheHudCarriesTheZone()
    {
        var content = Assert.IsType<HudContent.Reading>(
            Readout.Content(Reading(), UserSettings.Default, Friday, TwelveHour, Eastern));

        Assert.Equal("resets Mon 10:00 AM", Assert.Single(content.Lines).Reset);
    }

    [Fact]
    public void ThePanelCarriesTheZone()
    {
        var panel = PanelReadout.Compose(
            Reading(), UserSettings.Default, "0.3.0", Friday, TwelveHour, lastRefresh: null, Eastern);

        Assert.Equal("Mon 2 Nov 2026, 10:00 AM", Assert.Single(panel.Windows).Reset);
    }

    [Fact]
    public void TheNotificationCarriesTheZone()
    {
        var alert = new Alert(new AlertKey(WindowKind.WeeklyAll, 25, MondayReset), Severity.Warning, 80);

        var message = AlertMessage.Describe(alert, DisplayDirection.Consumed, Friday, TwelveHour, Eastern);

        Assert.Contains("resets Mon 10:00 AM", message.Body);
    }
}
