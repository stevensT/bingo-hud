# Codex Quota — Progress

updated: 2026-10-14
status: Phase 4 checkpoint passed 2026-10-14; Core work complete
blockers: none
next_session: Phase 5, the shell: 5.1 the first-start window, then tray check marks, HUD and panel drawing, and App wiring, each checked on screen with Trevor.

## 2026-10-14: the spike closed early

- Closed after two days of the planned seven, at Trevor's call: he uses Codex daily and wants the
  readout. The result, with the evidence against each decision row and the gaps accepted, is in
  `spikes/session-log-probe.md`.
- The session logs are the Codex source. AC-1 rewritten to name them, with what they cannot see.
- AC-3b, the remote icon, deferred past 0.5.0 (Trevor's call): there are no remote Codex readings
  without an endpoint source.
- `scripts/codex-ratelimit-probe.js` deleted, as the spike rules require.

## Checkpoints

### CP: Phase 1 Fixtures and the record parser — 2026-10-14
tests: 1515 pass / 0 fail / 0 skip (26 new in CodexRecordTests)
build: pass, 0 warnings, 0 errors (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 1.1, 1.2, 1.3
rework: none
criteria_met: none in full. Toward AC-1: a record is read from its structure after a text prefilter, windows by length, timed by its own timestamp, keeping only the rate-limit text. Toward AC-2: a null, broken or unrecognised record never yields a figure.
issues: none

### CP: Phase 2 Reading the logs — 2026-10-14
tests: 1551 pass / 0 fail / 0 skip (18 CodexLogReaderTests + 2 for NewestWrite, 16 CodexMonitorTests)
build: pass, 0 warnings, 0 errors (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 2.1, 2.2, 2.3, 2.4
rework: none
timing: against the real %USERPROFILE%\.codex, 42 logs, 30.4 MB, from a throwaway Release console program in the scratchpad, read-only, deleted after. First scan 58 ms, later scans 17 to 23 ms, the newest-write check 0.4 to 1.6 ms. Well under the one-second bar; the 60 s rescan costs about a millisecond when nothing has been written. The real reading it found: five-hour 52%, weekly 35%, from 9:20 PM.
criteria_met: none in full; the display halves are Phase 4. Core now does the source half of AC-1 (logs found under CODEX_HOME or .codex, newest record with figures, conversation lines never kept), of AC-3 (age from the record), of AC-3a (idle and failed told apart in CodexState), and of AC-7 (not found and no reading yet told apart).
issues: none

### CP: Phase 3 Products in the model — 2026-10-14
tests: 1582 pass / 0 fail / 0 skip (13 AlertProductTests, 11 ProductSettingsTests, 7 ProductDetectionTests)
build: pass, 0 warnings, 0 errors (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 3.1, 3.2, 3.3
rework: none
parallel group: run inline and in sequence, at Trevor's call. The three units were small, touched separate files, and finished in one sitting each; subagents would have cost more context than they saved.
guards: 6 of the 31 new tests passed before their change and are kept as guards rather than red-green tests: in 3.1 the product written by name and a 0.4.0 alerts file read as Claude's; in 3.2 no answer by default, no file, a 0.4.0 settings file, and an answer with nothing on.
fix during the phase: one 3.1 test had a hand-written reset four hours off its fixture (05:23 for 09:23 UTC), which would have kept it red for the wrong reason; corrected against the Unix time before implementing.
criteria_met: none in full; each needs Phase 4 or 5. In Core now: AC-10's alert identity (per product, ten-minute reset tolerance, 0.4.0 state kept as Claude's), AC-8's at-least-one rule, AC-8a's never-asked state surviving an upgrade, AC-8b's detection.
issues: none

### CP: Phase 4 Display in Core — 2026-10-14
tests: 1658 pass / 0 fail / 0 skip (15 ClaudeOnlyHudTests, 13 HudCompositionTests, 13 CodexSectionTests, 8 CodexStatusMessageTests, 18 CodexPanelTests, 9 CodexAlertTests)
build: pass, 0 warnings, 0 errors, whole solution including the app (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
rework: none
parallel group 4.5 and 4.6: run inline and in sequence, at Trevor's call, as Phase 3's was.
existing tests changed: only expected labels gained the product name, and nothing else in them: 14 in ReadoutTests (HUD lines, allowed by AC-9) and 7 in AlertMessageTests and AlertEngineTests (toasts, required by AC-10).
guards: 6 of the 76 new tests passed before their change and are kept as guards (3 Claude-only empty phrases in 4.1; in 4.3 two Codex empty phrases that 4.2's placeholders already told apart, and the live window beside an ended one).
criteria, Core side (each still needs the shell in Phase 5 before it is met in full):
- AC-1: logs read with conversation lines never kept (Phase 2); the panel states the source and what it cannot see (4.5).
- AC-2: no figure from a null, broken or unrecognised record, nor for a window whose reset has passed (4.3).
- AC-3: every Codex reading carries its age from its own record; an ended window shows no figure, raises no alert (4.3, 4.6).
- AC-3a: idle keeps colours with "used 12 min ago"; failed drops them and names the cause (4.3, 4.4).
- AC-3b: deferred past 0.5.0.
- AC-4, AC-5: one line builder for both products, following direction, reset format and bar (4.2).
- AC-6: Codex panel block with exact resets, age and last turn (4.5).
- AC-7: every Codex state without a reading has a phrase and advice; a shown product always has a section (4.2, 4.4).
- AC-7a: every HUD line and every notification carries its product (4.2, 4.6).
- AC-8, AC-8a, AC-8b: settings, never-asked state and detection in Core (Phase 3); the window and tray are 5.1, 5.2.
- AC-8c: shell only (5.2).
- AC-9: pinned by ClaudeOnlyHudTests against 0.4.0's recorded output; confirmed on screen in 5.3.
- AC-10: Codex alerts, product-named toasts, ended windows skipped (3.1, 4.6); wiring in 5.5.
issues: none
