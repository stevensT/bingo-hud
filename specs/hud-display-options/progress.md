# HUD Display Options — Progress

updated: 2026-10-01
status: Phase 4 checkpoint passed
blockers: none
next_session: start Phase 5 at 5.1 (changelog). Suite is 1452 green on a clean build.

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

### CP: Phase 4 On-screen verification and review — 2026-10-01
tests: 1452 pass / 0 fail / 0 skip (865 at Phase 3; 586 of the new cases are one boundary sweep)
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 4.1, 4.2, 4.3, 4.4
rework: none
criteria_met: all sixteen.
- AC-1, AC-2: countdown setting toggles, saves and reads back; "2.9h", "2.9d" on screen; boundaries
  and rounding swept in tests.
- AC-3: frozen reading kept its countdown beside "5 min old, sign-in expired" on screen.
- AC-4: null reset gives no countdown (tests).
- AC-5: the panel's exact times are untouched by this feature; `ResetFormatter.Exact` and the
  panel composer are unchanged.
- AC-6 to AC-10: bar setting, column placement, exact partial fill, fill always used, severity
  colours shared with the figure, all on screen in every severity.
- AC-11: frozen bar uncoloured with its figure, on screen.
- AC-12: no reading draws words, not a bar (tests; the shell draws bars only from lines).
- AC-13: 120% fills and stops, figure says 120%, on screen.
- AC-14: tray toggles, persistence, 0.1.0 and 0.2.0 files (tests and on screen).
- AC-15: collapse picks the same window in all four combinations (tests; not seen on screen, as
  the spike stated).
- AC-16: defaults captured identical to 0.1.0.
issues:
- Stale was assessed against tests, not on screen, as the spike stated before it ran.
- The last-segment gap change is a 2 px shell edit made after the captures; it will be seen at the
  BV.3 launch.
