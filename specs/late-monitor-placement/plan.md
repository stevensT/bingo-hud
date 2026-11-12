# Late Monitor Placement — Technical Plan

## Technical Approach

Today placement lives in the shell. `HudWindow.Restore` runs once, on `Loaded`: it tests the saved
position against the virtual screen (`HudPlacement.Fits`), asks Windows for the work area of the
monitor nearest the HUD's corner (`NativeMethods.WorkAreaAtCornerOf`), and snaps and keeps the HUD
inside it, or falls back to the top right of the primary work area. `Press` snaps a drop against
the same nearest work area and reports it through `_moved`, which `App.Remember` writes to disk.
The saved position is held in a `readonly` field set at construction, so even if `Restore` ran
again it would place the HUD from the position Bingo started with, not from the latest drop.

The decision moves into Core, where it can be tested against a described desk, and the shell
keeps only what needs Windows:

1. **Monitors as data.** The shell lists every connected monitor, its bounds and its work area in
   WPF units, and which one is primary. Core picks the nearest monitor to a point itself, by the
   rule Windows uses for `MONITOR_DEFAULTTONEAREST`: a monitor containing the point, otherwise the
   one whose bounds are closest.
2. **`HudPlacement.Place`**, a pure function from the saved position, the HUD's size and the
   monitor list to a placement: the position, and whether the HUD is away from its saved spot.
   It reproduces `Restore`'s rules exactly, so startup behaves as it does now.
3. **`HudMemory`**, a small Core class holding the saved position and the save callback. `Drop`
   snaps a drop, saves it at once and makes it the saved position. `Place` returns a placement
   from the current saved position and never saves. The HUD window holds one, so a later
   re-placement uses the latest drop.
4. **Re-placing on change.** The HUD window hooks its own window messages for `WM_DISPLAYCHANGE`
   and for `WM_SETTINGCHANGE` with `SPI_SETWORKAREA`, and re-places through `HudMemory.Place` one
   second after the last of a burst.
5. **The panel line.** The HUD window exposes whether it is away from its saved spot. `App` passes
   that to `PanelReadout`, which sets a new `PlacementNotice` on `PanelContent` from Core's
   wording. The detail panel shows it the way it shows `SettingsNotice`.

### Placement rules (unchanged at startup, now also after a change)

| Saved position | Placed at | Away |
|---|---|---|
| none (first run) | top right of the primary work area, inset | no |
| fits the bounding box of all monitors | snapped to an edge, then kept within the work area of the monitor nearest its corner | if its corner is on no monitor |
| does not fit | top right of the primary work area, inset | yes |

"On a monitor" allows the snap distance, as `Fits` does, so a HUD parked flush with an edge whose
corner sits a pixel outside after a font change is not reported as away.

On the reported desk with the portrait monitor missing, the saved corner (-347, 1511) fits the
box spanned by the strip display and the main monitor, lies on neither, is nearest the main
monitor, and is placed at x 0: the left edge of the main monitor, away. With the portrait monitor
connected, its corner lies on that monitor and it is placed at the portrait monitor's right edge,
not away.

## Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Where placement is decided | Core (`HudPlacement.Place`, `HudMemory`) | The test project references Core only, and `SeamTests` defends that. Leaving the decision in the WPF window would leave AC-1 to AC-5 untestable. |
| Nearest monitor | Computed in Core from a monitor list, replacing `WorkAreaAtCornerOf` | One way of answering "which monitor is the HUD on" for startup, drops, changes and growth. `StayOnScreen` moves to it too, so a drop and a resize never disagree about the monitor. |
| Change signal | The HUD window's own message hook: `WM_DISPLAYCHANGE`, and `WM_SETTINGCHANGE` with `SPI_SETWORKAREA` | WPF-native, tied to the window's lifetime, and nothing to unsubscribe. `SystemEvents.DisplaySettingsChanged` is a static event that outlives the window and adds no information. The work-area message covers a taskbar that settles after the monitor appears. |
| Debounce | Re-place 1 s after the last change message | A monitor connecting sends several messages, and the work areas can lag the first. Placing once at the end avoids moving the HUD on a half-settled layout. |
| No monitors listed | Shell falls back to the primary work area from `SystemParameters` as a single monitor | `Place` needs at least one monitor and throws without one. The fallback is what `WorkAreaUnderHud` does today when Windows cannot answer. |
| What counts as away | Saved corner on no monitor, within the snap distance | The same allowance `Fits` uses. A saved spot in the gap between staggered monitors also counts, and the panel line is true there: that spot is on no monitor. |
| Panel wording | "The HUD is not where you left it: that spot is on a monitor that is not connected. It will go back when the monitor connects." | Says what happened and that no action is needed. Dragging it elsewhere is implied by every drag saving. |
| Startup inset constant | `Inset` moves from `HudWindow` to Core beside `Place` | `Place` computes the fallback, so it owns the inset. |

Test-first is suspended for the native monitor listing and the WPF wiring (tasks 2.1 and 2.2):
there is no harness for either, and the precedent is audit-fixes 4.1 and scoped-cap-shape 2.1.
Both are thin: everything they pass along is decided in Core and tested there. They are verified
by hand under Build Verification, including disconnecting the portrait monitor in Windows display
settings and reconnecting it.

## Data Model

```csharp
// Core/Display
public sealed record DisplayMonitor(ScreenArea Bounds, ScreenArea WorkArea, bool IsPrimary);
public sealed record Placement(HudPosition Position, bool AwayFromSaved);

public static class HudPlacement
{
    public const double Inset = EdgeSnap.Distance + 8;                       // moved from HudWindow
    public static DisplayMonitor NearestMonitor(double x, double y, IReadOnlyList<DisplayMonitor> monitors);
    public static Placement Place(HudPosition? saved, double width, double height, IReadOnlyList<DisplayMonitor> monitors);
    // Fits and KeepWithin unchanged
}

public sealed class HudMemory
{
    public HudMemory(HudPosition? saved, Action<HudPosition> save);
    public HudPosition? Saved { get; }
    public HudPosition Drop(HudPosition dropped, double width, double height, IReadOnlyList<DisplayMonitor> monitors); // snaps, saves once, updates Saved
    public Placement Place(double width, double height, IReadOnlyList<DisplayMonitor> monitors);                       // never saves
}

public static class PlacementNotice { public static string? Describe(bool away); }
// PanelContent gains: string? PlacementNotice = null (included in equality)
// PanelReadout.ComposeAll gains: string? placementNotice = null, beside settingsNotice
```

The primary monitor's work area is the fallback area. A list with no monitor marked primary uses
the first.

## Interface

- `NativeMethods.Monitors()` returns each monitor's bounds and work area in physical pixels and
  its primary flag, through `EnumDisplayMonitors` and `GetMonitorInfo`. `WorkAreaAtCornerOf` is
  removed once nothing calls it. All Win32 stays in `NativeMethods`, as `InteropSeamTests` pins.
- `HudWindow` converts those to WPF units through its DPI transform (the existing `deferred:`
  note moves with it), and takes a `HudMemory` in place of `remembered` and `moved`. It exposes
  `bool AwayFromSaved`.
- `App` builds the `HudMemory` with `Remember(_settings with { Position = … })` as its save, and
  passes `hud.AwayFromSaved` into the panel content.

## Implementation Phases

1. **Core placement** — monitor record and nearest monitor, `Place`, `HudMemory`, panel notice.
2. **Shell** — monitor listing, HUD window wiring and change hook, app and panel wiring,
   changelog.
3. **Verification** — clean build and suite, then the disconnect and reconnect check by hand and
   a real sign-in.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| The late monitor is not the cause | The morning problem survives 0.6.1 | The fix re-places on any monitor change whatever the cause, and the panel line shows when the HUD is away and why. Trevor's first sign-in after installing is the real test. |
| The click-through tool window does not receive the change messages | Nothing re-places | Top-level windows receive both broadcasts. Verified by hand by disconnecting and reconnecting a monitor in display settings. |
| `Place` differs from today's `Restore` on some layout | Startup puts the HUD somewhere new | Tests pin today's three rules before the shell switches over, including the staggered gap that `Fits` documents. |
| A placement saves by accident | A missing monitor overwrites the user's spot, the bug this fixes made permanent | `HudMemory.Place` has no path to the save callback, and a test asserts it is never called across startup and change placements. |
| A re-place lands while the user is dragging | The HUD jumps under the cursor | The drag is modal and short, and the debounce is one second; the drop that ends it saves and becomes the saved spot, so the next re-place agrees with it. Accepted. |
