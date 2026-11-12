using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// Which monitor a point belongs to, by the rule Windows uses for a nearest-monitor lookup: the
/// monitor whose bounds contain the point, otherwise the one whose bounds are closest.
///
/// <para>
/// Placement needs this answer for a saved spot whose monitor may not be connected, so it is
/// worked out here from a list of monitors rather than asked of Windows about wherever the
/// window happens to be. Bounds include their left and top edges and exclude their right and
/// bottom ones, as Windows rectangles do, so a point on the seam between two side-by-side
/// monitors belongs to the one on the right.
/// </para>
/// </summary>
public class NearestMonitorTests
{
    [Fact]
    public void APointInsideAMonitorBelongsToIt()
    {
        Assert.Equal(ReportedDesk.Main, HudPlacement.NearestMonitor(1000, 500, ReportedDesk.AllConnected));
        Assert.Equal(ReportedDesk.Portrait, HudPlacement.NearestMonitor(-500, 1000, ReportedDesk.AllConnected));
        Assert.Equal(ReportedDesk.Strip, HudPlacement.NearestMonitor(-1300, 1000, ReportedDesk.AllConnected));
    }

    [Fact]
    public void APointOnTheSeamBelongsToTheMonitorOnTheRight()
    {
        Assert.Equal(ReportedDesk.Main, HudPlacement.NearestMonitor(0, 1000, ReportedDesk.AllConnected));
        Assert.Equal(ReportedDesk.Portrait, HudPlacement.NearestMonitor(-0.5, 1000, ReportedDesk.AllConnected));
    }

    [Fact]
    public void TheSavedSpotIsOnThePortraitMonitorWhenItIsConnected()
    {
        var spot = ReportedDesk.SavedSpot;

        Assert.Equal(ReportedDesk.Portrait, HudPlacement.NearestMonitor(spot.Left, spot.Top, ReportedDesk.AllConnected));
    }

    [Fact]
    public void TheSavedSpotBelongsToTheMainMonitorWhileThePortraitOneIsMissing()
    {
        // 347 from the main monitor's left edge, 733 from the strip display's right edge.
        var spot = ReportedDesk.SavedSpot;

        Assert.Equal(ReportedDesk.Main, HudPlacement.NearestMonitor(spot.Left, spot.Top, ReportedDesk.PortraitMissing));
    }

    [Fact]
    public void APointOutsideEveryMonitorBelongsToTheClosest()
    {
        // Above the portrait monitor, which starts at y 236: 136 away from it, and 500 from the
        // main monitor's left edge.
        Assert.Equal(ReportedDesk.Portrait, HudPlacement.NearestMonitor(-500, 100, ReportedDesk.AllConnected));
    }

    [Fact]
    public void APointEquallyFarFromTwoMonitorsBelongsToTheFirstListed()
    {
        var left = new DisplayMonitor(new ScreenArea(0, 0, 100, 100), new ScreenArea(0, 0, 100, 100), IsPrimary: true);
        var right = new DisplayMonitor(new ScreenArea(200, 0, 100, 100), new ScreenArea(200, 0, 100, 100), IsPrimary: false);

        // 150 is 50 past the left monitor's exclusive right edge at 100, and 50 short of the
        // right monitor's left edge at 200.
        Assert.Equal(left, HudPlacement.NearestMonitor(150, 50, [left, right]));
        Assert.Equal(right, HudPlacement.NearestMonitor(150, 50, [right, left]));
    }

    [Fact]
    public void ThereIsNoNearestMonitorWithoutAnyMonitors()
    {
        Assert.Throws<ArgumentException>(() => HudPlacement.NearestMonitor(0, 0, []));
    }
}
