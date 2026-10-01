# HUD Display Options — Progress

updated: 2026-10-01
status: Phase 1 checkpoint passed
blockers: none
next_session: start Phase 2 at 2.1. Suite is 819 green on a clean build.

## Checkpoints

### CP: Phase 1 Settings — 2026-10-01
tests: 819 pass / 0 fail / 0 skip (816 before the feature)
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 0.1, 1.1, 1.2
rework: none
criteria_met: AC-14 at the Core level (both settings persist and an older file loads); AC-16 for
settings (a 0.1.0 file loads to clock time and no bar). AC-1 and AC-6 have their settings but no
behaviour yet; that is Phase 2.
issues:
- 1.1 amended at start: an unknown `resetFormat` name resets the whole file, as an unknown
  `direction` already does, rather than resetting only itself. One rule for both enums.
