# Late Monitor Placement — Progress

updated: 2026-11-11
status: Phase 1 checkpoint passed
blockers: none
next_session: continue from the first incomplete task in tasks.md

## How it was found

After every sign-in the HUD came back at the left edge of the main monitor, at the height it had
been saved at, instead of at the right edge of the portrait monitor to its left where it had been
dropped. Quitting and relaunching by hand put it back correctly. The settings file held the
dropped position throughout. Windows listed the desk as a 515×1920 strip display at x -1595, a
1080×1920 portrait monitor at x -1080 and the 3840×2160 main monitor at x 0. Startup's rules put a
HUD saved at (-347, 1511) at exactly the reported spot only if the portrait monitor is missing and
the strip display is present.

Baseline before any change: 1747 pass.

## Checkpoints

### CP: Phase 1 Core placement — 2026-11-11
tests: 1786 pass / 0 fail / 0 skip
build: pass, 0 warnings (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 1.1, 1.2, 1.3, 1.4
criteria_met: AC-2, AC-3, AC-4, AC-7 in Core; AC-1, AC-5 and AC-6 in Core, with their shell wiring left to 2.2
notes:
- Run inline and in sequence, as the task list says; each task builds on the one before.
- Every task's tests were run against a stub throwing `NotImplementedException` first, so each failed on behaviour rather than on compilation: 7, 15, 10 and 7 failures.
- 1.2's test for the reported desk with the portrait monitor connected first expected the HUD snapped flush to x -329. The code was right and the test wrong: at the HUD's current width the saved spot leaves its right edge 18 short of the monitor's, outside the snap distance of 16, so today's startup rules leave it at -347. The test was corrected to the saved spot.
- 1.3's no-save rule was proven against a deliberate mutation: `HudMemory.Place` calling the save failed both no-save tests, and was reverted.
- 1.4's argument went on `PanelReadout.ComposeAll`, where `settingsNotice` already is, not on `Compose` as the plan first said; plan and task text corrected.
- `HudPlacement.Inset` now exists in Core beside the window's own copy; 2.2 removes the window's.
issues: none
