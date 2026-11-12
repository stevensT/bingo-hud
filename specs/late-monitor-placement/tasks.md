# Late Monitor Placement — Tasks

## Status Legend

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress
- `[P]` Parallelizable — run concurrently, or inline when small
- `[C]` Checkpoint — stop and verify before continuing

Task ids are stable. Each task cites the criteria it closes. Every test-and-implement task writes
its test first and watches it fail before the code exists.

## Build and test commands

See the Building section of `README.md`. Every checkpoint runs `dotnet clean` first, then
`dotnet build` and `dotnet test`.

---

## Phase 1: Core placement

These run in sequence: each builds on the one before, all in `Core/Display`.

- [x] 1.1 Test + implement: `DisplayMonitor` and `HudPlacement.NearestMonitor`. A point inside a
      monitor's bounds gives that monitor; a point inside none gives the monitor whose bounds are
      closest; a tie gives the first listed. Includes the reported desk: (-347, 1511) with the
      portrait monitor missing gives the main monitor, and with it present gives the portrait one.
      (AC-1)
- [x] 1.2 Test + implement: `HudPlacement.Place` and `Placement`, with `Inset` moved into Core.
      Pin today's rules first: no saved position goes to the top right of the primary work area,
      not away; a saved position that fits is snapped and kept within the nearest monitor's work
      area; one that does not fit goes to the top right, away; the staggered gap `Fits` documents
      is pulled onto the nearest monitor. Then the away rule: corner on no monitor, within the
      snap distance, is away; a corner a pixel outside a monitor's edge is not. The reported desk
      in both states: left edge of the main monitor and away, then the portrait monitor's right
      edge and not away. A list with no primary uses the first; an empty list throws.
      (AC-1, AC-2, AC-7)
- [x] 1.3 Test + implement: `HudMemory`. `Drop` returns the drop snapped against the nearest
      monitor's work area, calls the save exactly once with that snapped position before it
      returns, and makes it `Saved`. `Place` never calls the save, across a startup placement, a
      placement with the saved monitor missing, and one after it returns. After a `Drop` while
      away, `Place` places from the new spot and is not away. (AC-3, AC-4, AC-5)
- [x] 1.4 Test + implement: `PlacementNotice.Describe`, null when not away and the plan's wording
      when away. `PanelContent.PlacementNotice`, included in equality, and a `placementNotice`
      argument on `PanelReadout.ComposeAll`, beside `settingsNotice`, that sets it. (AC-6)
- [x] 1.5 Checkpoint: clean, build, full suite; audit marks; AC-1 to AC-7 as far as Core goes;
      record in `progress.md`.

## Phase 2: Shell

These run in sequence: all touch the HUD window or what it calls.

- [ ] 2.1 Add `NativeMethods.Monitors()` through `EnumDisplayMonitors` and `GetMonitorInfo`,
      returning bounds, work area and the primary flag in physical pixels. Test-first suspended:
      no harness for Win32 (see the plan). `InteropSeamTests` must still pass. (AC-1)
- [ ] 2.2 Wire the HUD window. Take a `HudMemory` in place of `remembered` and `moved`; convert
      the monitor list to WPF units, falling back to the primary work area as a single monitor
      when the list is empty; make `Restore`, `Press` and `StayOnScreen` use `HudMemory` and
      `HudPlacement` with that list; hook `WM_DISPLAYCHANGE` and `WM_SETTINGCHANGE` with
      `SPI_SETWORKAREA` and re-place one second after the last; expose `AwayFromSaved`. Remove
      `WorkAreaAtCornerOf` and the window's `Inset`. In `App`, build the `HudMemory` with
      `Remember` as its save, and pass the notice into the panel content; show it in
      `DetailPanelWindow` beside `SettingsNotice`. Test-first suspended as in 2.1; Core decides
      everything this passes along. (AC-1 to AC-6)
- [ ] 2.3 Update the doc comments that describe placement as a startup-only check: `HudWindow`'s
      class summary and `Restore`, `HudPlacement.Fits`, and the `StayOnScreen` remarks. Add a
      Fixed entry under Unreleased in `CHANGELOG.md`. (AC-8)
- [ ] 2.4 Checkpoint: clean, build, full suite; audit marks; all acceptance criteria; record in
      `progress.md`.

## Build Verification

- [ ] `dotnet clean`, then `dotnet build` with zero warnings
- [ ] `dotnet test`, all passing
- [ ] Run the Debug build with all three monitors connected: the HUD comes up at its saved spot,
      and the panel shows no placement line
- [ ] In Windows display settings, disconnect the portrait monitor: within a couple of seconds
      the HUD moves onto a connected monitor and the panel shows the placement line; the settings
      file's position and time are unchanged
- [ ] Reconnect it: the HUD returns to the portrait monitor's right edge, the line goes, and the
      settings file is still unchanged
- [ ] Disconnect again, drag the HUD somewhere on the main monitor, reconnect: the HUD stays where
      it was dropped, the line is gone, and the settings file holds the new spot
- [ ] Quit from the tray and relaunch: the HUD comes up at the saved spot
- [ ] Final checkpoint recorded in `progress.md`; review findings resolved or deferred here

The sign-in check, the morning case this exists for, needs a release installed at sign-in and a
restart. It is recorded under the 0.6.1 release in `progress.md`, not here.
