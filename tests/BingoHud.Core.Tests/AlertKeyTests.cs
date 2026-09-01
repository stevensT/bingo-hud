using BingoHud.Core.Alerts;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// What makes two alerts the same alert.
///
/// <para>
/// An alert fires once per threshold per window occurrence. "Occurrence" is the part that needs
/// a name: the session window at 10% remaining today and the session window at 10% remaining
/// tomorrow are different alerts, and the only thing separating them is which reset they belong
/// to. Carrying <c>resets_at</c> in the identity means rearming after a reset is not a mechanism
/// at all — the key simply stops matching anything already recorded, and the alert is armed
/// again by construction.
/// </para>
/// <para>
/// The corollary is that a window with no reset time has no occurrence, and so cannot be
/// deduplicated. Such a window yields no key at all rather than a key that would fire on every
/// poll forever.
/// </para>
/// </summary>
public class AlertKeyTests
{
    private static readonly DateTimeOffset Reset =
        new(2026, 8, 31, 16, 0, 0, TimeSpan.FromHours(-7));

    private static QuotaWindow Window(
        WindowKind kind = WindowKind.Session,
        DateTimeOffset? resetsAt = null) =>
        new(kind, UsedPercent: 92, resetsAt, ServerSeverity.Normal);

    [Fact]
    public void TheSameWindowOccurrenceAtTheSameThresholdIsTheSameKey()
    {
        var first = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset);
        var second = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ANewResetTimeIsANewKeySoTheAlertRearmsWithoutAnyMechanism()
    {
        var thisWindow = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset);
        var nextWindow = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset.AddHours(5));

        Assert.NotEqual(thisWindow, nextWindow);
    }

    [Fact]
    public void EachThresholdIsItsOwnKeySoWarningAndCriticalBothGetToFire()
    {
        var warning = new AlertKey(WindowKind.Session, ThresholdPercent: 25, Reset);
        var critical = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset);

        Assert.NotEqual(warning, critical);
    }

    [Fact]
    public void EachWindowKindIsItsOwnKeySoTheTwoWindowsAlertIndependently()
    {
        var session = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset);
        var weekly = new AlertKey(WindowKind.WeeklyAll, ThresholdPercent: 10, Reset);

        Assert.NotEqual(session, weekly);
    }

    [Fact]
    public void TheSameInstantWrittenInADifferentOffsetIsTheSameKey()
    {
        // Persisted state is read back as whatever offset the store wrote. If identity depended
        // on the spelling rather than the instant, a restart would rearm every alert.
        var local = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset);
        var utc = new AlertKey(WindowKind.Session, ThresholdPercent: 10, Reset.ToUniversalTime());

        Assert.Equal(local, utc);
        Assert.Equal(local.GetHashCode(), utc.GetHashCode());
    }

    [Fact]
    public void AWindowWithAResetTimeYieldsAKeyCarryingThatOccurrence()
    {
        var key = AlertKey.For(Window(WindowKind.WeeklyAll, Reset), thresholdPercent: 25);

        Assert.Equal(new AlertKey(WindowKind.WeeklyAll, 25, Reset), key);
    }

    [Fact]
    public void AWindowWithNoResetTimeYieldsNoKeyAndSoCannotAlert()
    {
        // Observed in the wild: a window reports a utilization with resets_at null. It has no
        // occurrence to be once-per, so it gets no key rather than one that fires every poll.
        Assert.Null(AlertKey.For(Window(resetsAt: null), thresholdPercent: 10));
    }
}
