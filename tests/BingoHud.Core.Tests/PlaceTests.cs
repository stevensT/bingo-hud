using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// Where the HUD goes when Bingo starts, and again whenever the monitor setup changes, and
/// whether that is somewhere other than where the user left it.
///
/// <para>
/// The first group pins the rules the HUD window applied at startup before placement moved into
/// Core, so that moving it changed nothing about where a HUD comes up: no saved spot goes to the
/// top right of the primary monitor; a saved spot inside the box around every monitor is snapped
/// to an edge and kept within the nearest monitor; anything else goes to the top right.
/// </para>
/// <para>
/// The second group is new: a HUD is away from its saved spot when that spot's corner is on no
/// connected monitor, allowing the snap distance as <see cref="HudPlacement.Fits"/> does. Away
/// is what the detail panel reports, and what makes the HUD worth placing again when a monitor
/// connects.
/// </para>
/// </summary>
public class PlaceTests
{
    private const double Width = 220;
    private const double Height = 48;

    // Two side-by-side 1920×1080 monitors, the primary on the right at the origin, as in
    // HudPlacementTests. The taskbar takes the bottom 40 of the primary.
    private static readonly DisplayMonitor Primary = new(
        new ScreenArea(0, 0, 1920, 1080), new ScreenArea(0, 0, 1920, 1040), IsPrimary: true);

    private static readonly DisplayMonitor LeftOfPrimary = new(
        new ScreenArea(-1920, 0, 1920, 1080), new ScreenArea(-1920, 0, 1920, 1080), IsPrimary: false);

    private static readonly IReadOnlyList<DisplayMonitor> SideBySide = [Primary, LeftOfPrimary];

    private static HudPosition TopRightOf(ScreenArea work, double width) =>
        new(work.Left + work.Width - width - HudPlacement.Inset, work.Top + HudPlacement.Inset);

    // Today's rules.

    [Fact]
    public void WithNoSavedSpotTheHudGoesToTheTopRightOfThePrimaryMonitor()
    {
        var placed = HudPlacement.Place(null, Width, Height, SideBySide);

        Assert.Equal(new Placement(TopRightOf(Primary.WorkArea, Width), AwayFromSaved: false), placed);
    }

    [Fact]
    public void ThePrimaryMonitorIsFoundWhereverItIsListed()
    {
        var placed = HudPlacement.Place(null, Width, Height, [LeftOfPrimary, Primary]);

        Assert.Equal(TopRightOf(Primary.WorkArea, Width), placed.Position);
    }

    [Fact]
    public void WithNoMonitorMarkedPrimaryTheFirstListedStandsInForIt()
    {
        var unmarked = LeftOfPrimary with { IsPrimary = false };
        var other = Primary with { IsPrimary = false };

        var placed = HudPlacement.Place(null, Width, Height, [unmarked, other]);

        Assert.Equal(TopRightOf(unmarked.WorkArea, Width), placed.Position);
    }

    [Fact]
    public void ASavedSpotOnAMonitorIsUsedAsItIs()
    {
        var saved = new HudPosition(100, 100);

        Assert.Equal(new Placement(saved, AwayFromSaved: false), HudPlacement.Place(saved, Width, Height, SideBySide));
    }

    [Fact]
    public void ASavedSpotOnTheSecondMonitorIsUsedAsItIs()
    {
        var saved = new HudPosition(-1000, 400);

        Assert.Equal(new Placement(saved, AwayFromSaved: false), HudPlacement.Place(saved, Width, Height, SideBySide));
    }

    [Fact]
    public void ASavedSpotNearAnEdgeIsSnappedFlushWithIt()
    {
        // 10 short of the primary's right edge, within the snap distance.
        var saved = new HudPosition(1920 - Width - 10, 100);

        Assert.Equal(new HudPosition(1920 - Width, 100), HudPlacement.Place(saved, Width, Height, SideBySide).Position);
    }

    [Fact]
    public void ASavedSpotIsSnappedToTheWorkAreaNotTheWholeMonitor()
    {
        // Parked flush on the taskbar, which is at 1040, not at the monitor's bottom at 1080.
        var saved = new HudPosition(500, 1040 - Height + 5);

        Assert.Equal(new HudPosition(500, 1040 - Height), HudPlacement.Place(saved, Width, Height, SideBySide).Position);
    }

    [Fact]
    public void ASavedSpotOffEveryMonitorGoesToTheTopRightAndIsAway()
    {
        var placed = HudPlacement.Place(new HudPosition(5000, 5000), Width, Height, SideBySide);

        Assert.Equal(new Placement(TopRightOf(Primary.WorkArea, Width), AwayFromSaved: true), placed);
    }

    [Fact]
    public void ASavedSpotThatIsNotANumberGoesToTheTopRightAndIsAway()
    {
        var placed = HudPlacement.Place(new HudPosition(double.NaN, 100), Width, Height, SideBySide);

        Assert.Equal(new Placement(TopRightOf(Primary.WorkArea, Width), AwayFromSaved: true), placed);
    }

    [Fact]
    public void ASavedSpotInTheGapBetweenStaggeredMonitorsIsPulledOntoTheNearestOne()
    {
        // The layout HudWindow.StayOnScreen documents: a portrait monitor right of the primary,
        // starting lower. (2000, 100) is inside the box around both and on neither: 80 right of
        // the primary, 200 above the portrait monitor.
        var portrait = new DisplayMonitor(
            new ScreenArea(1920, 300, 1080, 1920), new ScreenArea(1920, 300, 1080, 1920), IsPrimary: false);

        var placed = HudPlacement.Place(new HudPosition(2000, 100), Width, Height, [Primary, portrait]);

        Assert.Equal(new HudPosition(1920 - Width, 100), placed.Position);
    }

    // Away.

    [Fact]
    public void ACornerAPixelPastAMonitorsEdgeIsNotAway()
    {
        // Parked flush left on the left monitor, then drifted a pixel past it: within the snap
        // distance of the monitor, and snapped back flush.
        var placed = HudPlacement.Place(new HudPosition(-1921, 100), Width, Height, SideBySide);

        Assert.Equal(new Placement(new HudPosition(-1920, 100), AwayFromSaved: false), placed);
    }

    [Fact]
    public void ASavedSpotInTheGapBetweenStaggeredMonitorsIsAway()
    {
        // The spot is on no monitor, so the HUD is not where it was left, and the panel line is
        // true: that spot is on no connected monitor.
        var portrait = new DisplayMonitor(
            new ScreenArea(1920, 300, 1080, 1920), new ScreenArea(1920, 300, 1080, 1920), IsPrimary: false);

        Assert.True(HudPlacement.Place(new HudPosition(2000, 100), Width, Height, [Primary, portrait]).AwayFromSaved);
    }

    [Fact]
    public void WhileThePortraitMonitorIsMissingTheHudLandsOnTheMainMonitorsLeftEdgeAndIsAway()
    {
        // The reported morning: the saved spot fits the box spanned by the strip display and
        // the main monitor, lies on neither, and is kept within the main monitor at x 0.
        var placed = HudPlacement.Place(
            ReportedDesk.SavedSpot, ReportedDesk.HudWidth, ReportedDesk.HudHeight, ReportedDesk.PortraitMissing);

        Assert.Equal(new Placement(new HudPosition(0, 1511), AwayFromSaved: true), placed);
    }

    [Fact]
    public void OnceThePortraitMonitorIsConnectedTheHudIsBackAtItsRightEdgeAndNotAway()
    {
        // Exactly the saved spot. It was dropped flush when the HUD was 347 wide; at 329 its
        // right edge stops 18 short of the monitor's, outside the snap distance, so it is not
        // pulled flush. Today's startup rules do the same.
        var placed = HudPlacement.Place(
            ReportedDesk.SavedSpot, ReportedDesk.HudWidth, ReportedDesk.HudHeight, ReportedDesk.AllConnected);

        Assert.Equal(new Placement(ReportedDesk.SavedSpot, AwayFromSaved: false), placed);
    }

    [Fact]
    public void ThereIsNoPlacementWithoutAnyMonitors()
    {
        Assert.Throws<ArgumentException>(() => HudPlacement.Place(new HudPosition(0, 0), Width, Height, []));
    }
}
