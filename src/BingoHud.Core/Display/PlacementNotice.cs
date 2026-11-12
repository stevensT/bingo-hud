namespace BingoHud.Core.Display;

/// <summary>
/// The panel's line for a HUD that is not where the user left it because that spot is on no
/// connected monitor (late-monitor-placement AC-6), or null when it is where they left it.
/// </summary>
/// <remarks>
/// It says that nothing needs doing: the HUD goes back by itself when the monitor connects, and
/// a drag elsewhere is remembered like any other. It is shown only while it is true, and no file
/// is written for it.
/// </remarks>
public static class PlacementNotice
{
    public static string? Describe(bool away) => away
        ? "The HUD is not where you left it: that spot is on a monitor that is not connected. "
          + "It will go back when the monitor connects."
        : null;
}
