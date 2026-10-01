# HUD Display Options — Tasks

## Status Legend

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress
- `[P]` Parallelizable — executed concurrently as subagents, or inline when small
- `[C]` Checkpoint — stop and verify before continuing

Task ids are stable. Acceptance criteria from `spec.md` are cited per task so checkpoint audits
have something concrete to check against. Every test-and-implement task writes its test first and
watches it fail before the implementation exists.

## Build and test commands

See the Building section of `README.md`. Every checkpoint runs `dotnet clean` first, then
`dotnet build` and `dotnet test`.

## Prerequisite

- [ ] 0.1 `v0.1.0` is tagged on `d08d170`. Trevor's step; this task only confirms it with
      `git tag` before any code changes, so 0.2.0 work cannot leak into the 0.1.0 release.

---

## Phase 1: Settings

- [ ] 1.1 Test + implement the two new settings: `ResetFormat` (`ClockTime`, `Countdown`) and
      `ShowBar` on `UserSettings`, defaulting to `ClockTime` and `false`. Tests: defaults are
      0.1.0's behaviour; both fields round-trip through `SettingsStore` save and load; an unknown
      `resetFormat` value loads as the default rather than resetting the whole file. (AC-1, AC-6,
      AC-14)
- [ ] 1.2 Test: a literal 0.1.0 `settings.json` (position, collapse, direction, thresholds, and
      neither new key) loads with every old field kept and both new ones defaulted. Implement only
      if 1.1 has not already made it pass. (AC-14, AC-16)
- [ ] 1.3 Checkpoint: clean, build, full suite; audit marks; record in `progress.md`.

## Phase 2: Core display logic

2.1 and 2.2 share no files and can run side by side. They are small pure functions, so they may
be done inline in sequence instead; say which at 2.5.

- [P] 2.1 Test + implement `ResetFormatter.Countdown(resetsAt, now, culture)`. Tests, each at
      both sides of its boundary: null reset gives null (AC-4); ≥ 24h gives days ("1.0d" at
      exactly 24h, "4.5d"); ≥ 1h gives hours ("23.9h" at 23h 59m, "1.0h" at exactly 1h); below
      that, whole minutes rounded down ("59m", "1m"); under a minute and at or past the reset
      give "<1m"; tenths always shown, including ".0"; rounded down, never up ("4.9d" at 4d 23h);
      a comma-decimal culture gives "4,5d". (AC-2, AC-4)
- [P] 2.2 Test + implement `Bar.Segments(usedPercent)`: ten values, each 0.0 to 1.0. Tests: 0
      gives all zero; 100 gives all one; 75 gives seven ones, a half, and two zeros; 3 gives 0.3
      then zeros; above 100 matches 100; below 0 matches 0; NaN matches 0. (AC-8, AC-13)
- [ ] 2.3 Test + implement `ReadoutLine.Bar` (null, or the ten values from 2.2) with equality
      written out so two lines with equal bars built separately compare equal, and lines whose
      bars differ in one segment do not. Test through `HudContent.SameAs` as well, since that is
      the comparison the shell trusts to skip repaints. (AC-8; plan risk 1)
- [ ] 2.4 Test + implement `Readout` reading the two settings. Tests:
      - countdown mode gives the countdown phrase, clock-time mode is byte-identical to 0.1.0
        for the same state (AC-1, AC-16);
      - rate-limited keeps its "limited" prefix in countdown mode (quota-hud AC-6);
      - a stale and a frozen reading keep their countdown and carry the same mark as in
        clock-time mode (AC-3);
      - bar off gives `Bar` null; bar on gives the segments for the window's used figure;
      - the bar fills with used in both directions, while the figure's wording still follows
        the direction setting (AC-7, AC-9);
      - a frozen line's severity, and so its bar colour, is normal (AC-11);
      - no reading, and an error with no earlier reading, give `HudContent.Empty` with or
        without the bar (AC-12);
      - collapse picks the same window in all four combinations (AC-15).
- [ ] 2.5 Checkpoint: clean, build, full suite; audit marks; check AC-1 to AC-4, AC-7 to AC-9,
      AC-11 to AC-13, AC-15 and AC-16 at the Core level; record in `progress.md`, including how
      2.1 and 2.2 were run.

## Phase 3: Shell

The WPF layer has no tests. Anything here that could be wrong in a way a test would catch belongs
in Core instead; if a task here starts computing, stop and move the computation.

- [ ] 3.1 Add two tray menu items next to "Collapse" and "Show percentage remaining": "Show reset
      as countdown" and "Show bar". Both `CheckOnClick`; checkmarks read from settings when the
      menu opens, like the existing two. (AC-1, AC-6, AC-14)
- [ ] 3.2 Draw the bar in `HudWindow`: a column between the window label and the figure, present
      only when the line's `Bar` is not null. Ten segment boxes on a dark track, each filled to
      its fraction from the left, the fill coloured by the line's severity using the existing
      brushes, dimmed white when normal. (AC-7, AC-8, AC-10, AC-11)
- [ ] 3.3 Checkpoint: clean, build, full suite; launch the built exe against the live account and
      toggle both settings from the tray; record in `progress.md`.

## Phase 4: On-screen verification

- [ ] 4.1 Restore `scripts/stub-usage-server.js` from git history and write
      `specs/hud-display-options/spikes/display-options-onscreen.md`: what each capture is meant
      to show, written before the captures are taken. Test-first is suspended for this spike
      because its output is evidence, not a component.
- [ ] 4.2 Capture the HUD from the built exe in all four setting combinations, at normal,
      warning, critical, rate-limited, over 100, stale, and frozen. Check each against the spike
      document: bar and figure the same colour, partial segment visible, marks present, nothing
      off screen at the right edge, width change on toggle handled. (AC-3, AC-7 to AC-13, AC-15)
- [ ] 4.3 Close the spike: record the result in its document and delete the stub script.
- [ ] 4.4 Run the review on the feature's changes and resolve or defer each finding, recording
      deferrals here.
- [ ] 4.5 Checkpoint: clean, build, full suite; every acceptance criterion assessed against the
      spec; record in `progress.md`.

## Phase 5: Release prep

- [ ] 5.1 Add `[Unreleased]` entries to `CHANGELOG.md` for both settings. Update the README if it
      describes what the HUD shows.
- [ ] 5.2 Set `<Version>` to `0.2.0`, move the entries into a dated `0.2.0` section, and confirm
      the version the panel displays. The tag is Trevor's.

## Build Verification

- [ ] BV.1 `dotnet clean` then `dotnet build`: 0 warnings, 0 errors.
- [ ] BV.2 `dotnet test`: full suite, no failures, no unexplained skips.
- [ ] BV.3 Publish both shapes per the README's Publishing section and launch each from a path
      outside the repository; confirm the HUD reads the live account with both settings on.
- [ ] BV.4 Final checkpoint: all green, every acceptance criterion met, recorded in
      `progress.md`.
