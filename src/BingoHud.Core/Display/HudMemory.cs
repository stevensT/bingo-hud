using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>
/// The spot the user left the HUD at (AC-22), and the only way it changes: a drop.
///
/// <para>
/// <see cref="Drop"/> snaps a drop to an edge of the monitor it landed on (AC-20), writes it
/// down at once through the save it was given, and makes it the spot every later placement
/// starts from. Nothing waits for Bingo to close, so quitting from the tray, a shutdown, or the
/// setup closing Bingo for an upgrade loses no drop.
/// </para>
/// <para>
/// <see cref="Place"/> has no path to the save. A monitor missing at sign-in may move the HUD
/// onto another one, but must never move the spot it goes back to when that monitor connects.
/// </para>
/// </summary>
public sealed class HudMemory
{
    private readonly Action<HudPosition> _save;

    public HudMemory(HudPosition? saved, Action<HudPosition> save)
    {
        Saved = saved;
        _save = save;
    }

    /// <summary>Where the user last dropped the HUD, or null if they never have.</summary>
    public HudPosition? Saved { get; private set; }

    /// <summary>
    /// Snaps a drop to the work area of the monitor nearest its corner, saves it, and returns
    /// where the HUD should sit.
    /// </summary>
    public HudPosition Drop(HudPosition dropped, double width, double height, IReadOnlyList<DisplayMonitor> monitors)
    {
        var work = HudPlacement.NearestMonitor(dropped.Left, dropped.Top, monitors).WorkArea;
        var snapped = EdgeSnap.Snap(dropped, width, height, work);

        Saved = snapped;
        _save(snapped);

        return snapped;
    }

    /// <summary>Where the HUD goes on these monitors, from the saved spot. Saves nothing.</summary>
    public Placement Place(double width, double height, IReadOnlyList<DisplayMonitor> monitors) =>
        HudPlacement.Place(Saved, width, height, monitors);
}
