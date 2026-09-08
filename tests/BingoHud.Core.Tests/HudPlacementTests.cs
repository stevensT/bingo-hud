using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// Whether a remembered HUD position is still somewhere the user can see.
///
/// <para>
/// A laptop undocked from an external monitor keeps a settings file that points at a screen
/// that is no longer there. Restoring that position puts the HUD nowhere, with no way to drag
/// it back and no sign that it is running at all. The rule is that a remembered position is
/// used only if the whole HUD would land inside the screen area; otherwise the shell places it
/// afresh, as on first run.
/// </para>
/// <para>
/// "Inside" allows an overshoot of up to the snap distance. A HUD parked flush with an edge is
/// the common case, and a font or DPI change between runs can widen it by a pixel; treating
/// that as a missing monitor would throw the position away for nothing. The shell snaps a
/// restored position, so an overshoot that fits is also pulled back flush.
/// </para>
/// <para>
/// Units are device-independent, as WPF reports them. Negative coordinates are ordinary on a
/// monitor left of or above the primary one.
/// </para>
/// </summary>
public class HudPlacementTests
{
    // Two side-by-side 1920×1080 monitors, the left one secondary, so the area starts at -1920.
    private static readonly ScreenArea Screens = new(Left: -1920, Top: 0, Width: 3840, Height: 1080);
    private const double Width = 220;
    private const double Height = 48;

    [Theory]
    [InlineData(100, 100)]
    [InlineData(-1920, 0)]
    [InlineData(1920 - 220, 1080 - 48)]
    [InlineData(1920 - 220 + 16, 100)]     // overshoots the right edge by the snap distance
    [InlineData(-1936, 100)]               // overshoots the left edge by the snap distance
    [InlineData(100, 1080 - 48 + 1)]       // one unit past the bottom: the DPI-drift case
    public void APositionWhollyOnScreenOrWithinSnappingReachOfItStillFits(double left, double top)
    {
        Assert.True(HudPlacement.Fits(new HudPosition(left, top), Width, Height, Screens));
    }

    [Theory]
    [InlineData(1920 - 220 + 17, 100)]
    [InlineData(-1937, 100)]
    [InlineData(100, -17)]
    [InlineData(100, 1080 - 48 + 17)]
    [InlineData(5000, 5000)]
    public void APositionFurtherOffScreenThanSnappingCanRecoverDoesNotFit(double left, double top)
    {
        Assert.False(HudPlacement.Fits(new HudPosition(left, top), Width, Height, Screens));
    }

    [Fact]
    public void ANonFinitePositionDoesNotFit()
    {
        Assert.False(HudPlacement.Fits(new HudPosition(double.NaN, 100), Width, Height, Screens));
    }

    // The readout resizes the HUD as its text changes: a placeholder becomes two lines, and
    // "resets 11:34 AM" becomes "resets Thu 11:34 AM". A HUD parked at a right or bottom edge
    // grows past it, so after a resize it is pulled back just far enough to stay in view.
    // KeepWithin clamps to whatever area it is given and does nothing else; in the app that
    // area is the monitor the HUD is on. Position is not re-saved for this; only a drop is,
    // so the user's anchor survives.

    [Fact]
    public void AHudThatStillFitsAfterGrowingIsLeftWhereItIs()
    {
        var position = new HudPosition(100, 100);

        Assert.Equal(position, HudPlacement.KeepWithin(position, 300, 60, Screens));
    }

    [Fact]
    public void AHudThatGrewPastTheRightEdgeIsPulledBackFlush()
    {
        // Parked flush right at 200 wide, then widened to 350.
        var parked = new HudPosition(1920 - 200, 24);

        Assert.Equal(new HudPosition(1920 - 350, 24), HudPlacement.KeepWithin(parked, 350, 60, Screens));
    }

    [Fact]
    public void AHudThatGrewPastTheBottomEdgeIsPulledUpFlush()
    {
        var parked = new HudPosition(24, 1080 - 30);

        Assert.Equal(new HudPosition(24, 1080 - 60), HudPlacement.KeepWithin(parked, 300, 60, Screens));
    }

    [Fact]
    public void ANegativeCoordinateInsideTheAreaIsLeftAlone()
    {
        // Ordinary on a monitor left of the primary.
        var position = new HudPosition(-1000, 100);

        Assert.Equal(position, HudPlacement.KeepWithin(position, 300, 60, Screens));
    }

    [Fact]
    public void AHudPastTheLeftEdgeIsPulledBackFlush()
    {
        Assert.Equal(
            new HudPosition(-1920, 100),
            HudPlacement.KeepWithin(new HudPosition(-1930, 100), 300, 60, Screens));
    }

    [Fact]
    public void AHudWiderThanTheAreaKeepsItsLeftEdgeVisible()
    {
        // No position fits. The top-left corner is what the user can grab, so that is the
        // corner that stays on screen.
        Assert.Equal(
            new HudPosition(-1920, 0),
            HudPlacement.KeepWithin(new HudPosition(0, 0), 5000, 3000, Screens));
    }

    [Fact]
    public void AnUnplacedPositionPassesThroughKeepWithin()
    {
        // Pinned for consistency with Fits: NaN in, NaN out, and the shell moves nothing.
        var kept = HudPlacement.KeepWithin(new HudPosition(double.NaN, double.NaN), 300, 60, Screens);

        Assert.True(double.IsNaN(kept.Left));
        Assert.True(double.IsNaN(kept.Top));
    }
}
