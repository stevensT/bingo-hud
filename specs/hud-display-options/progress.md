# HUD Display Options — Progress

updated: 2026-10-01
status: Phase 3 checkpoint passed
blockers: none
next_session: start Phase 4 at 4.1 (restore the stub server as a spike). Suite is 865 green on a
clean build.

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

### CP: Phase 2 Core display logic — 2026-10-01
tests: 865 pass / 0 fail / 0 skip (819 at Phase 1)
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 2.1, 2.2, 2.3, 2.4
rework: none
criteria_met (Core level; the shell draws none of it yet): AC-1, AC-2, AC-3, AC-4, AC-7, AC-8,
AC-9, AC-11 (bar colour follows the line's severity, normal when frozen), AC-12, AC-13, AC-15,
AC-16. AC-5 and AC-14 unchanged from 0.1.0 and Phase 1; AC-6 and AC-10 wait on the shell.
issues:
- 2.1 and 2.2 ran inline and in sequence, not as subagents: two small pure functions in separate
  files, cheaper to write than to brief.
- 2.1 was not watched red before its implementation existed. Compensated by a mutation check:
  switching floor to round turned five of its tests red.
- The plan worried that flooring a double would misround on exact tenths. Checked against integer
  arithmetic at every tenth boundary up to seven days, and at every whole minute: no disagreement,
  so the plain `Math.Floor` form stands and the reason is recorded on the method.
- 2.3 confirmed the record-equality trap on screen: with the default equality, the two tests about
  separately built bars went red; with equality written out, green.
- One 2.4 test expectation was wrong, not the code: 75% used is 25% left, exactly the warning
  line, so 0.1.0 draws it as a warning. Expectation corrected.

### CP: Phase 3 Shell — 2026-10-01
tests: 865 pass / 0 fail / 0 skip (no change; the shell has no tests by design)
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 3.1, 3.2
rework: none
criteria_met: AC-1, AC-6, AC-14 on screen (both tray items toggle, save, and load on relaunch);
AC-16 on screen (defaults captured identical in layout to 0.1.0); AC-7 and AC-8 on screen at
normal severity (`5h [bar] 23% used 2.1h`, two full segments and a sliver of the third). Severity
colours, rate-limited, stale and frozen on screen are Phase 4's.
issues:
- Found on screen and fixed: full segments drew with square corners inside the rounded track,
  because clipping cuts to the rectangle rather than the rounded outline. The fill now carries
  the track's corners itself, rounded left-only when partial.
- The HUD grew from 310 to 339 px wide with both options on and stayed on screen at the right
  edge. Checked again across states in Phase 4.
- Verification ran with the trial HUD stopped and `%LOCALAPPDATA%\Bingo` backed up; the backup was
  restored and the trial exe relaunched afterwards.
