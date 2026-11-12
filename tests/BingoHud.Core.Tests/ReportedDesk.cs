using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// The three-monitor desk the late-monitor bug was reported on, in device-independent units, as
/// Windows listed it on 2026-11-11. Left to right: a strip display, a portrait monitor, and the
/// 4K main monitor at the origin. The portrait monitor is the one taken to connect late at
/// sign-in.
/// </summary>
/// <remarks>
/// Only the main monitor's taskbar is modelled, docked at the bottom; the others are given work
/// areas equal to their bounds. The HUD saved on this desk sat at the portrait monitor's right
/// edge, at <see cref="SavedSpot"/>.
/// </remarks>
internal static class ReportedDesk
{
    public static readonly DisplayMonitor Strip = new(
        Bounds: new ScreenArea(-1595, 235, 515, 1920),
        WorkArea: new ScreenArea(-1595, 235, 515, 1920),
        IsPrimary: false);

    public static readonly DisplayMonitor Portrait = new(
        Bounds: new ScreenArea(-1080, 236, 1080, 1920),
        WorkArea: new ScreenArea(-1080, 236, 1080, 1920),
        IsPrimary: false);

    public static readonly DisplayMonitor Main = new(
        Bounds: new ScreenArea(0, 0, 3840, 2160),
        WorkArea: new ScreenArea(0, 0, 3840, 2112),
        IsPrimary: true);

    /// <summary>All three connected, in the order Windows listed them.</summary>
    public static readonly IReadOnlyList<DisplayMonitor> AllConnected = [Main, Portrait, Strip];

    /// <summary>Early in sign-in: the portrait monitor not yet connected.</summary>
    public static readonly IReadOnlyList<DisplayMonitor> PortraitMissing = [Main, Strip];

    /// <summary>
    /// Where the HUD was saved: dropped flush with the portrait monitor's right edge while the
    /// HUD was 347 wide.
    /// </summary>
    public static readonly HudPosition SavedSpot = new(-347, 1511);

    /// <summary>The HUD's size on that desk when the bug was looked at.</summary>
    public const double HudWidth = 329;
    public const double HudHeight = 117;
}
