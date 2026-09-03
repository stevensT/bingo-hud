using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>
/// The rectangle every monitor together covers, in device-independent units. What WPF calls
/// the virtual screen.
/// </summary>
public sealed record ScreenArea(double Left, double Top, double Width, double Height);

/// <summary>
/// Whether a remembered HUD position is still somewhere the user can see.
///
/// <para>
/// A remembered position is used only if the whole HUD would land inside the screen area, or
/// within snapping reach of it. A laptop undocked from its external monitor otherwise restores
/// the HUD to nowhere, with no way to drag it back and no sign it is running.
/// </para>
/// <para>
/// The snapping allowance is for a HUD parked flush with an edge, which is the common case: a
/// font or DPI change between runs can widen it by a pixel, and that is not a missing monitor.
/// The shell snaps what it restores, so the overshoot is pulled back flush.
/// </para>
/// </summary>
public static class HudPlacement
{
    // deferred: the screen area is the bounding box of all monitors, so a position in the gap
    // between two staggered monitors passes. Ask each monitor's own rectangle if that ever bites.
    public static bool Fits(HudPosition position, double width, double height, ScreenArea screens) =>
        double.IsFinite(position.Left) && double.IsFinite(position.Top)
        && position.Left >= screens.Left - EdgeSnap.Distance
        && position.Top >= screens.Top - EdgeSnap.Distance
        && position.Left + width <= screens.Left + screens.Width + EdgeSnap.Distance
        && position.Top + height <= screens.Top + screens.Height + EdgeSnap.Distance;
}
