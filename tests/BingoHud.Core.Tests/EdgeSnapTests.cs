using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// Where a dropped HUD ends up (AC-20): flush with a screen edge if it was dropped near one,
/// exactly where it was dropped otherwise.
///
/// <para>
/// "Near" is bounded on both sides of the edge. Unbounded overshoot would be simpler, but a
/// second monitor to the left of the primary has every position to the left of the primary's
/// edge, and unbounded snapping would yank a HUD off it. Bounding the distance is what makes
/// the rule safe on more than one screen without knowing anything about the second one.
/// </para>
/// </summary>
public class EdgeSnapTests
{
    // A 1920×1080 primary with a 40-unit taskbar at the bottom, so the work area is 1040 tall.
    private static readonly ScreenArea Work = new(Left: 0, Top: 0, Width: 1920, Height: 1040);
    private const double Width = 220;
    private const double Height = 48;

    private static HudPosition Snap(double left, double top) =>
        EdgeSnap.Snap(new HudPosition(left, top), Width, Height, Work);

    [Theory]
    [InlineData(10, 300, 0, 300)]                          // near the left edge
    [InlineData(-10, 300, 0, 300)]                         // overshot the left edge
    [InlineData(1920 - 220 - 10, 300, 1920 - 220, 300)]    // near the right edge
    [InlineData(1920 - 220 + 10, 300, 1920 - 220, 300)]    // overshot the right edge
    [InlineData(500, 12, 500, 0)]                          // near the top
    [InlineData(500, 1040 - 48 - 5, 500, 1040 - 48)]       // near the bottom, above the taskbar
    [InlineData(1920 - 220 - 3, 6, 1920 - 220, 0)]         // a corner snaps on both axes
    public void ADropWithinTheSnapDistanceOfAnEdgeLandsFlushWithIt(
        double left, double top, double expectedLeft, double expectedTop)
    {
        Assert.Equal(new HudPosition(expectedLeft, expectedTop), Snap(left, top));
    }

    [Theory]
    [InlineData(500, 300)]          // the middle of the screen
    [InlineData(17, 300)]           // one unit outside the snap distance
    [InlineData(-17, 300)]          // one unit too far overshot
    [InlineData(-1000, 300)]        // on a monitor to the left: none of the primary's business
    public void ADropAnywhereElseStaysExactlyWhereItWas(double left, double top)
    {
        Assert.Equal(new HudPosition(left, top), Snap(left, top));
    }

    [Fact]
    public void TheSnapDistanceIsSixteenUnits()
    {
        // Pinned because the shell's default placement has to sit outside it, or the first
        // nudge of a freshly placed HUD would jump it flush.
        Assert.Equal(16, EdgeSnap.Distance);
        Assert.Equal(new HudPosition(0, 300), Snap(16, 300));
    }
}
