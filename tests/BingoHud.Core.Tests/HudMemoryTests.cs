using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// The spot the user left the HUD at, and the only way it changes: a drop.
///
/// <para>
/// A drop is snapped to an edge of the monitor it landed on, written down at once through the
/// save it was given, and becomes the spot every later placement starts from. Placing the HUD,
/// at startup or after a monitor change, never writes anything. That is the point of the
/// feature: a monitor missing at sign-in may move the HUD, but must never move the spot it goes
/// back to.
/// </para>
/// </summary>
public class HudMemoryTests
{
    private const double Width = ReportedDesk.HudWidth;
    private const double Height = ReportedDesk.HudHeight;

    private readonly List<HudPosition> _saves = [];

    private HudMemory Remembering(HudPosition? saved) => new(saved, _saves.Add);

    [Fact]
    public void ADropIsSnappedToTheEdgeOfTheMonitorItLandedOn()
    {
        // The right edge of the HUD 11 short of the portrait monitor's right edge.
        var memory = Remembering(null);

        var kept = memory.Drop(new HudPosition(-340, 1000), Width, Height, ReportedDesk.AllConnected);

        Assert.Equal(new HudPosition(-Width, 1000), kept);
    }

    [Fact]
    public void ADropIsSnappedToTheTaskbarNotTheMonitorsBottom()
    {
        // The main monitor's work area ends at 2112, above its taskbar.
        var memory = Remembering(null);

        var kept = memory.Drop(new HudPosition(1000, 2112 - Height - 6), Width, Height, ReportedDesk.AllConnected);

        Assert.Equal(new HudPosition(1000, 2112 - Height), kept);
    }

    [Fact]
    public void ADropAwayFromAnyEdgeIsKeptWhereItLanded()
    {
        var memory = Remembering(null);

        var kept = memory.Drop(new HudPosition(1000, 800), Width, Height, ReportedDesk.AllConnected);

        Assert.Equal(new HudPosition(1000, 800), kept);
    }

    [Fact]
    public void ADropIsSavedOnceAsSnappedBeforeDropReturns()
    {
        var memory = Remembering(null);

        var kept = memory.Drop(new HudPosition(-340, 1000), Width, Height, ReportedDesk.AllConnected);

        Assert.Equal([kept], _saves);
    }

    [Fact]
    public void ADropBecomesTheSavedSpot()
    {
        var memory = Remembering(ReportedDesk.SavedSpot);

        var kept = memory.Drop(new HudPosition(1000, 800), Width, Height, ReportedDesk.AllConnected);

        Assert.Equal(kept, memory.Saved);
    }

    [Fact]
    public void TheSavedSpotStartsAsTheOneItWasGiven()
    {
        Assert.Equal(ReportedDesk.SavedSpot, Remembering(ReportedDesk.SavedSpot).Saved);
        Assert.Null(Remembering(null).Saved);
    }

    [Fact]
    public void PlacingTheHudNeverSavesAnything()
    {
        // Startup with all three monitors, a sign-in with the portrait one missing, and its
        // return: the morning this feature exists for, start to finish.
        var memory = Remembering(ReportedDesk.SavedSpot);

        memory.Place(Width, Height, ReportedDesk.AllConnected);
        memory.Place(Width, Height, ReportedDesk.PortraitMissing);
        memory.Place(Width, Height, ReportedDesk.AllConnected);

        Assert.Empty(_saves);
        Assert.Equal(ReportedDesk.SavedSpot, memory.Saved);
    }

    [Fact]
    public void TheHudGoesBackToItsSavedSpotWhenTheMissingMonitorConnects()
    {
        var memory = Remembering(ReportedDesk.SavedSpot);

        var atSignIn = memory.Place(Width, Height, ReportedDesk.PortraitMissing);
        var connected = memory.Place(Width, Height, ReportedDesk.AllConnected);

        Assert.Equal(new Placement(new HudPosition(0, 1511), AwayFromSaved: true), atSignIn);
        Assert.Equal(new Placement(ReportedDesk.SavedSpot, AwayFromSaved: false), connected);
    }

    [Fact]
    public void ADropWhileAwayIsTheNewSpotAndTheOldOneIsNotReturnedTo()
    {
        var memory = Remembering(ReportedDesk.SavedSpot);
        memory.Place(Width, Height, ReportedDesk.PortraitMissing);

        var kept = memory.Drop(new HudPosition(1000, 800), Width, Height, ReportedDesk.PortraitMissing);

        Assert.Equal(new Placement(kept, AwayFromSaved: false), memory.Place(Width, Height, ReportedDesk.PortraitMissing));
        Assert.Equal(new Placement(kept, AwayFromSaved: false), memory.Place(Width, Height, ReportedDesk.AllConnected));
    }

    [Fact]
    public void WithNothingSavedTheHudIsPlacedAsOnFirstRun()
    {
        var placed = Remembering(null).Place(Width, Height, ReportedDesk.AllConnected);

        Assert.Equal(HudPlacement.Place(null, Width, Height, ReportedDesk.AllConnected), placed);
        Assert.Empty(_saves);
    }
}
