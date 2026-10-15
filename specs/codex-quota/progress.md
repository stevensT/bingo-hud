# Codex Quota — Progress

updated: 2026-10-14
status: Phase 4 in progress; 4.1 and 4.2 done 2026-10-14
blockers: none
next_session: task 4.3, the Codex section from CodexState: phrases for not found and no reading, the age mark, idle vs failed, and the reset-passed line.

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
