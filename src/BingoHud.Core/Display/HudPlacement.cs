using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>
/// A rectangle of screen, in device-independent units. Which rectangle is the caller's choice:
/// the virtual screen for <see cref="HudPlacement.Fits"/>, the work area of one monitor for
/// <see cref="EdgeSnap.Snap"/> and <see cref="HudPlacement.KeepWithin"/>.
/// </summary>
public sealed record ScreenArea(double Left, double Top, double Width, double Height);

/// <summary>
/// Where a HUD of a given size may sit: whether a remembered spot still counts as on screen,
/// and where a HUD that outgrew its spot is pulled back to.
/// </summary>
public static class HudPlacement
{
    /// <summary>
    /// Whether a remembered HUD position is still somewhere the user can see.
    ///
    /// <para>
    /// A remembered position is used only if the whole HUD would land inside the screen area,
    /// or within snapping reach of it. A laptop undocked from its external monitor otherwise
    /// restores the HUD to nowhere, with no way to drag it back and no sign it is running.
    /// </para>
    /// <para>
    /// The snapping allowance is for a HUD parked flush with an edge, which is the common case:
    /// a font or DPI change between runs can widen it by a pixel, and that is not a missing
    /// monitor. The shell snaps what it restores, so the overshoot is pulled back flush.
    /// </para>
    /// <para>
    /// The screen area is the bounding box of all monitors, so a position in the gap between
    /// two staggered monitors passes. The shell answers that by also running a restored
    /// position through <see cref="KeepWithin"/> against the nearest monitor, so a HUD that
    /// passes here is still pulled onto a real screen.
    /// </para>
    /// </summary>
    public static bool Fits(HudPosition position, double width, double height, ScreenArea screens) =>
        double.IsFinite(position.Left) && double.IsFinite(position.Top)
        && position.Left >= screens.Left - EdgeSnap.Distance
        && position.Top >= screens.Top - EdgeSnap.Distance
        && position.Left + width <= screens.Left + screens.Width + EdgeSnap.Distance
        && position.Top + height <= screens.Top + screens.Height + EdgeSnap.Distance;

    /// <summary>
    /// The nearest position at which a HUD of this size lies wholly inside the area.
    ///
    /// <para>
    /// For a HUD that changed size where it stood. The readout resizes it whenever its text
    /// changes, and one parked at a right or bottom edge grows past that edge; this pulls it
    /// back only as far as it has to go, and leaves one that still fits exactly where it was.
    /// A HUD larger than the area keeps its top-left corner on screen and overflows the far
    /// edge: no position fits, and the corner is what the user can grab.
    /// </para>
    /// </summary>
    public static HudPosition KeepWithin(HudPosition position, double width, double height, ScreenArea screens)
    {
        var left = Math.Max(screens.Left, Math.Min(position.Left, screens.Left + screens.Width - width));
        var top = Math.Max(screens.Top, Math.Min(position.Top, screens.Top + screens.Height - height));

        return new HudPosition(left, top);
    }
}
