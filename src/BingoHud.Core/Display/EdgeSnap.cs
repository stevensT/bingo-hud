using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>
/// Where a dropped HUD ends up (AC-20): flush with an edge of the area if it was dropped within
/// <see cref="Distance"/> of one, on either side of it, and exactly where it was dropped
/// otherwise.
///
/// <para>
/// The distance is bounded on both sides of the edge on purpose. A monitor to the left of the
/// primary has every position to the left of the primary's edge, and an unbounded rule would
/// yank a HUD off it.
/// </para>
/// </summary>
public static class EdgeSnap
{
    public const double Distance = 16;

    // deferred: the area is the primary work area, so the far edges of a second monitor do not
    // snap. Ask the monitor under the window for its own work area if that ever matters.
    public static HudPosition Snap(HudPosition dropped, double width, double height, ScreenArea area)
    {
        var right = area.Left + area.Width;
        var bottom = area.Top + area.Height;

        var left = Near(dropped.Left, area.Left) ? area.Left
            : Near(dropped.Left + width, right) ? right - width
            : dropped.Left;

        var top = Near(dropped.Top, area.Top) ? area.Top
            : Near(dropped.Top + height, bottom) ? bottom - height
            : dropped.Top;

        return new HudPosition(left, top);
    }

    private static bool Near(double edgeOfHud, double edgeOfArea) =>
        Math.Abs(edgeOfHud - edgeOfArea) <= Distance;
}
