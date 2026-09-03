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
}
