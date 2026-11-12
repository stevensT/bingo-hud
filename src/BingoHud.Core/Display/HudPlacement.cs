using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>
/// A rectangle of screen, in device-independent units. Which rectangle is the caller's choice:
/// the virtual screen for <see cref="HudPlacement.Fits"/>, the work area of one monitor for
/// <see cref="EdgeSnap.Snap"/> and <see cref="HudPlacement.KeepWithin"/>.
/// </summary>
public sealed record ScreenArea(double Left, double Top, double Width, double Height);

/// <summary>
/// One connected monitor: its whole rectangle, the part of it not taken by the taskbar, and
/// whether Windows calls it the primary monitor. Device-independent units, like
/// <see cref="ScreenArea"/>.
/// </summary>
public sealed record DisplayMonitor(ScreenArea Bounds, ScreenArea WorkArea, bool IsPrimary);

/// <summary>
/// Where the HUD is put, and whether that is away from the spot the user left it at because
/// that spot is on no connected monitor.
/// </summary>
public sealed record Placement(HudPosition Position, bool AwayFromSaved);

/// <summary>
/// Where a HUD of a given size may sit: whether a remembered spot still counts as on screen,
/// and where a HUD that outgrew its spot is pulled back to.
/// </summary>
public static class HudPlacement
{
    /// <summary>
    /// How far a HUD with no usable saved spot sits from the primary work area's top and right
    /// edges. Outside the snap distance, so a fresh HUD does not jump flush on its first nudge.
    /// </summary>
    public const double Inset = EdgeSnap.Distance + 8;

    /// <summary>
    /// Where to put a HUD of this size, given the spot the user left it at and the monitors
    /// connected now. Used at startup, and again whenever the monitor setup changes.
    ///
    /// <para>
    /// With no saved spot, the HUD goes to the top right of the primary work area. A saved spot
    /// that <see cref="Fits"/> the box around every monitor is snapped to an edge and kept
    /// within the work area of the monitor nearest its corner. Any other saved spot goes to the
    /// top right too. These are the rules the HUD window applied at startup before they moved
    /// here.
    /// </para>
    /// <para>
    /// The placement is away from the saved spot when that spot's corner is on no connected
    /// monitor, allowing the snap distance as <see cref="Fits"/> does, so a HUD that drifted a
    /// pixel past an edge is not away. A spot in the gap between staggered monitors is away
    /// too: it is on no monitor, and the HUD is pulled onto one.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The list is empty; the shell always passes at least one monitor.</exception>
    public static Placement Place(HudPosition? saved, double width, double height, IReadOnlyList<DisplayMonitor> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("There is nowhere to place the HUD without any monitors.", nameof(monitors));
        }

        if (saved is { } spot && Fits(spot, width, height, BoxAround(monitors)))
        {
            var work = NearestMonitor(spot.Left, spot.Top, monitors).WorkArea;
            var placed = KeepWithin(EdgeSnap.Snap(spot, width, height, work), width, height, work);

            return new Placement(placed, AwayFromSaved: !IsOnAMonitor(spot, monitors));
        }

        var primary = (monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0]).WorkArea;
        var topRight = new HudPosition(primary.Left + primary.Width - width - Inset, primary.Top + Inset);

        return new Placement(topRight, AwayFromSaved: saved is not null);
    }

    /// <summary>The bounding box of every monitor: what Windows calls the virtual screen.</summary>
    private static ScreenArea BoxAround(IReadOnlyList<DisplayMonitor> monitors)
    {
        var left = monitors.Min(m => m.Bounds.Left);
        var top = monitors.Min(m => m.Bounds.Top);
        var right = monitors.Max(m => m.Bounds.Left + m.Bounds.Width);
        var bottom = monitors.Max(m => m.Bounds.Top + m.Bounds.Height);

        return new ScreenArea(left, top, right - left, bottom - top);
    }

    /// <summary>Whether a spot's corner is on some monitor, or within the snap distance of one.</summary>
    private static bool IsOnAMonitor(HudPosition spot, IReadOnlyList<DisplayMonitor> monitors) =>
        monitors.Any(m =>
            spot.Left >= m.Bounds.Left - EdgeSnap.Distance
            && spot.Left < m.Bounds.Left + m.Bounds.Width + EdgeSnap.Distance
            && spot.Top >= m.Bounds.Top - EdgeSnap.Distance
            && spot.Top < m.Bounds.Top + m.Bounds.Height + EdgeSnap.Distance);

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

    /// <summary>
    /// The monitor a point belongs to: the one whose bounds contain it, otherwise the one whose
    /// bounds are closest, by the rule Windows uses for a nearest-monitor lookup.
    ///
    /// <para>
    /// Worked out from a list rather than asked of Windows, because placement needs the answer
    /// for a saved spot whose own monitor may not be connected, not for wherever the window
    /// happens to be. Bounds include their left and top edges and exclude their right and
    /// bottom ones, as Windows rectangles do, so a point on the seam between two monitors
    /// belongs to exactly one of them. Of two monitors equally close, the first listed wins.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">The list is empty; every caller has at least one monitor.</exception>
    public static DisplayMonitor NearestMonitor(double x, double y, IReadOnlyList<DisplayMonitor> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("There is no nearest monitor without any monitors.", nameof(monitors));
        }

        var nearest = monitors[0];
        var nearestDistance = double.PositiveInfinity;

        foreach (var monitor in monitors)
        {
            var distance = DistanceSquared(x, y, monitor.Bounds);
            if (distance < nearestDistance)
            {
                nearest = monitor;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Zero for a point inside the area, its left and top edges included and its right and
    /// bottom ones not; otherwise the squared straight-line distance to the area's nearest edge
    /// or corner. Squared, because only the order matters.
    /// </summary>
    private static double DistanceSquared(double x, double y, ScreenArea area)
    {
        var right = area.Left + area.Width;
        var bottom = area.Top + area.Height;

        var inside = x >= area.Left && x < right && y >= area.Top && y < bottom;
        if (inside)
        {
            return 0;
        }

        var dx = x < area.Left ? area.Left - x : x >= right ? x - right : 0;
        var dy = y < area.Top ? area.Top - y : y >= bottom ? y - bottom : 0;

        return dx * dx + dy * dy;
    }
}
