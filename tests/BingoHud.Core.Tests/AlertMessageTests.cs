using System.Globalization;
using BingoHud.Core.Alerts;
using BingoHud.Core.Display;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The words a desktop notification carries (AC-14).
///
/// <para>
/// <see cref="Alert"/> holds a decision and no wording on purpose, so this is where a crossing
/// becomes a sentence. It matters more here than anywhere else in the app: a toast is read once,
/// out of context, often while the user is doing something else, and it cannot be gone back to
/// once dismissed. It has to say which window, how much is left, and when that changes.
/// </para>
/// <para>
/// The percentage reads in whichever direction the user set (AC-2a, AC-2b). A toast saying "22%"
/// while the HUD two inches away says "78%" would be the app contradicting itself at the one
/// moment the user is paying attention to it.
/// </para>
/// </summary>
public class AlertMessageTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");

    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 14, 30, 0, TimeSpan.FromHours(-7));

    private static Alert Alert(
        WindowKind kind = WindowKind.Session,
        int threshold = 25,
        Severity severity = Severity.Warning,
        double used = 78,
        TimeSpan? until = null) =>
        new(
            new AlertKey(kind, threshold, Now + (until ?? TimeSpan.FromMinutes(53))),
            severity,
            used);

    private static AlertMessage Describe(
        Alert alert,
        DisplayDirection direction = DisplayDirection.Remaining) =>
        AlertMessage.Describe(alert, direction, Now, TwelveHour);

    [Fact]
    public void AWarningNamesTheWindowAndSaysItIsRunningLow()
    {
        Assert.Equal("5h window running low", Describe(Alert()).Title);
    }

    [Fact]
    public void ACriticalSaysSomethingStrongerThanAWarning()
    {
        // The two must not read alike. A user who cannot tell them apart at a glance has three
        // severity states on the HUD and one in their notifications.
        var critical = Alert(threshold: 10, severity: Severity.Critical, used: 92);

        Assert.Equal("5h window nearly used up", Describe(critical).Title);
    }

    [Fact]
    public void TheWeeklyWindowIsNamedTheWayTheHudNamesIt()
    {
        var weekly = Alert(kind: WindowKind.WeeklyAll);

        Assert.Equal("Week window running low", Describe(weekly).Title);
    }

    [Fact]
    public void TheBodyGivesTheFigureAndWhenTheWindowResets()
    {
        Assert.Equal("22% left, resets in 53 min.", Describe(Alert()).Body);
    }

    [Fact]
    public void TheFigureReadsTheWayTheUserSetIt()
    {
        // The stored value is consumed, and the user asked to see consumed. A toast that
        // disagreed with the HUD would be the app contradicting itself.
        var body = Describe(Alert(), DisplayDirection.Consumed).Body;

        Assert.Equal("78% used, resets in 53 min.", body);
    }

    [Fact]
    public void ADistantResetIsGivenAsAClockTimeRatherThanACountdown()
    {
        // The same rule and the same phrasing the HUD follows: "resets in 1800 min" says nothing
        // a person can use. The day is named but the date is not, which is enough here because
        // no window Bingo tracks is more than a week long, and it is what the HUD says too.
        var distant = Alert(kind: WindowKind.WeeklyAll, until: TimeSpan.FromHours(30));

        Assert.Equal("22% left, resets Sun 8:30 PM.", Describe(distant).Body);
    }

    [Fact]
    public void AnAlertQuotesTheReadingRatherThanTheThresholdItCrossed()
    {
        // 78% used crossed the line drawn at 25% remaining. The number worth showing is where
        // the account actually stands, not where the line was.
        var alert = Alert(threshold: 25, used: 81);

        Assert.Equal("19% left, resets in 53 min.", Describe(alert).Body);
    }

    // ---- More than one crossing at once ----
    //
    // Observed rather than imagined: with the warning line moved so both windows crossed on one
    // reading, Windows showed the first notification and silently dropped the second, while the
    // engine recorded both as fired. The second crossing was therefore lost for that occurrence,
    // which is exactly what AC-14 exists to prevent. One notification carries them all.

    [Fact]
    public void OneAlertIsAnnouncedOnItsOwnTerms()
    {
        var message = AlertMessage.Describe([Alert()], DisplayDirection.Remaining, Now, TwelveHour);

        Assert.Equal("5h window running low", message.Title);
        Assert.Equal("22% left, resets in 53 min.", message.Body);
    }

    [Fact]
    public void TwoAlertsAreCountedInTheTitleRatherThanRacingEachOther()
    {
        var both = new[] { Alert(), Alert(kind: WindowKind.WeeklyAll, used: 80) };

        var message = AlertMessage.Describe(both, DisplayDirection.Remaining, Now, TwelveHour);

        Assert.Equal("2 windows running low", message.Title);
    }

    [Fact]
    public void EveryWindowIsNamedInTheBodyWhenSeveralCrossedAtOnce()
    {
        // The title no longer says which windows, so each line has to.
        var both = new[] { Alert(), Alert(kind: WindowKind.WeeklyAll, used: 80) };

        var message = AlertMessage.Describe(both, DisplayDirection.Remaining, Now, TwelveHour);

        Assert.Equal(
            "5h: 22% left, resets in 53 min." + Environment.NewLine +
            "Week: 20% left, resets in 53 min.",
            message.Body);
    }

    [Fact]
    public void AMixedBatchTakesItsTitleFromTheWorstOfThem()
    {
        // Erring toward the more urgent word. A title that called a critical crossing "running
        // low" would understate it, and the body still says which is which.
        var mixed = new[]
        {
            Alert(),
            Alert(kind: WindowKind.WeeklyAll, threshold: 10, severity: Severity.Critical, used: 93),
        };

        var message = AlertMessage.Describe(mixed, DisplayDirection.Remaining, Now, TwelveHour);

        Assert.Equal("2 windows nearly used up", message.Title);
    }

    [Fact]
    public void AnEmptyBatchHasNothingToAnnounce()
    {
        Assert.Null(AlertMessage.Describe([], DisplayDirection.Remaining, Now, TwelveHour));
    }
}
