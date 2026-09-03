using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// When the HUD is solid and when it lets clicks through (AC-21).
///
/// <para>
/// The HUD receives no mouse input while it is click-through, so nothing here is driven by
/// mouse events. The shell polls the cursor position on a timer and feeds each observation in;
/// this decides. A cursor that rests on the HUD for the dwell time makes it solid; one that
/// passes over it, or clicks without pausing, does not. Solid lasts until the cursor leaves,
/// and leaving restarts the clock.
/// </para>
/// </summary>
public class DwellPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset After(double ms) => T0.AddMilliseconds(ms);

    [Fact]
    public void TheDwellIsFourHundredMilliseconds()
    {
        // Long enough that a cursor crossing the HUD never trips it; short enough that stopping
        // on it does not feel like waiting.
        Assert.Equal(TimeSpan.FromMilliseconds(400), DwellPolicy.Dwell);
    }

    [Fact]
    public void ACursorElsewhereLeavesTheHudClickThrough()
    {
        var policy = new DwellPolicy();

        Assert.False(policy.Update(cursorOverHud: false, T0));
        Assert.False(policy.Update(cursorOverHud: false, After(5000)));
    }

    [Fact]
    public void ArrivingIsNotEnoughAndRestingForTheDwellIs()
    {
        var policy = new DwellPolicy();

        Assert.False(policy.Update(true, T0));
        Assert.False(policy.Update(true, After(399)));
        Assert.True(policy.Update(true, After(400)));
    }

    [Fact]
    public void OnceSolidItStaysSolidWhileTheCursorStays()
    {
        var policy = new DwellPolicy();
        policy.Update(true, T0);
        policy.Update(true, After(400));

        Assert.True(policy.Update(true, After(60_000)));
    }

    [Fact]
    public void LeavingMakesItClickThroughAtOnce()
    {
        var policy = new DwellPolicy();
        policy.Update(true, T0);
        policy.Update(true, After(400));

        Assert.False(policy.Update(false, After(401)));
    }

    [Fact]
    public void ComingBackStartsTheDwellAgain()
    {
        var policy = new DwellPolicy();
        policy.Update(true, T0);
        policy.Update(true, After(400));
        policy.Update(false, After(500));

        Assert.False(policy.Update(true, After(600)));
        Assert.False(policy.Update(true, After(999)));
        Assert.True(policy.Update(true, After(1000)));
    }

    [Fact]
    public void PassingOverOnTheWayToSomethingBeneathNeverTurnsSolid()
    {
        var policy = new DwellPolicy();

        Assert.False(policy.Update(true, T0));
        Assert.False(policy.Update(true, After(100)));
        Assert.False(policy.Update(false, After(200)));
    }
}
