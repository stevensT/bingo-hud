# Codex Quota — Progress

updated: 2026-10-15
status: Stopped for the night; 5.5 and 5.6 complete; 5.8 incomplete after failed on-screen notice check
blockers: task 5.8 on-screen failure — changing Show bar while the settings file was intended to be locked changed the HUD but showed no settings-save notice
next_session: resume task 5.8. Re-run the full suite and build, then reproduce the missing settings-save notice with a confirmed exclusive lock and the correct dev instance. Verify notice appearance and clearing before completing 5.8. Then run the clean/build/full-suite checkpoint 5.7, assess acceptance criteria, and continue Phase 6.

## 2026-10-15: resuming Phase 5

- Resume baseline: full build passed with 0 warnings/errors, full suite 1667 pass. The test
  runner stalled in the restricted environment; the same suite outside it passed. Builds used
  one worker and no shared compiler.
- Task 5.5 completed; six delivery tests added, all seen red before implementation. Review
  findings resolved: overlapping product notifications wait for the current notification to end;
  mute discards pending crossings. Comment corrections resolved. Trevor later saw both synthetic
  product notifications appear in sequence, confirming the actual Windows callbacks.
- Task 5.6 completed with Trevor: not found, no reading, live 52%/35% with blue bars, unreadable
  logs retaining figures with frozen styling, and an ended 5h window losing only its figure/bar.
  Native desktop inspection did not expose Bingo's windows, so on-screen evidence is Trevor's
  direct confirmation.
- The real comparison found a bug: app 75% remaining versus Bingo 16% used. Newer quota records
  were in open files with older modification times. Removed both metadata shortcuts: every
  requested rescan reads, and the newest record timestamp wins across logs. Unreadable records
  retain their timestamp when available, so an older malformed record does not hide a newer
  reading; an untimed malformed record is conservative. Five regression cases observed red
  before the fix. Added the optional reader-level untimed-record guard after review. All review
  findings resolved. Full real scans measured 115–167 ms. Trevor confirmed both real windows
  agreed after refreshing the corrected build.
- The original shared files were restored between runs. The current settings differed from the
  older 5.1 backup in position, so 5.6 restored its fresh backup instead. A separate fresh backup
  for the current settings trial is artifacts/settings-backup-5.8.
- Task 5.8 code complete: SettingsStore retries the first I/O failure once after 100 ms; failure
  becomes an independent panel notice naming the file and saying changes apply until Bingo
  closes; the next successful save clears it. Eight tests observed red before implementation,
  one first-attempt-success guard already passed. Code, failure-handling, test, type, comment and
  simplification reviews completed with no behavior findings; one stale panel summary corrected.
  Full suite 1683 pass / 0 fail / 0 skip, build 0 warnings/errors.
- STOPPING POINT: Trevor reported that unchecking Show bar changed the setting but no notice
  appeared. Task 5.8 is therefore incomplete despite the passing tests and reviews. No cause has
  been established. A diagnostic call was interrupted before its results were assessed; do not
  assume it confirmed the lock, the running instance, or the notice path. The source currently
  passes the notice from Remember through ComposeAll into the panel, so start by reproducing
  the actual screen failure rather than guessing at a code change.
- NEXT REPRO: back up the current shared files afresh before another trial. Close the installed
  build while testing, launch the new dev build, open its panel, then hold the actual settings
  file exclusively and independently confirm that a write-open fails. Toggle Show bar; check
  the save result and panel notice. Release the lock, make a different settings change, verify
  the notice clears and both changes reach disk. Native inspection did not expose Bingo's
  windows in this session; Trevor provided the visual checks.
- CLEANUP COMPLETED: the settings lock was released; dev process 53728 was stopped; settings.json
  and alerts.json were restored from artifacts/settings-backup-5.8. SHA-256 checks confirmed
  both files exactly match that backup. The installed 0.4.0 build was restarted. No trial is
  intentionally left running. Both notification test servers were stopped earlier.
- VERIFICATION LIMIT: the last full suite was 1683 pass / 0 fail / 0 skip. The last incremental
  build passed and reported 0 warnings/errors. The mandatory clean checkpoint 5.7 has NOT run;
  do not treat the warm build as that checkpoint. Phase 6, publish shapes, setup, and release
  verification have not started. No staging, commits, or pushes were performed.

## 2026-10-14: where the session stopped

- Everything through 5.4 is committed by Trevor except the last chunk (5.4 and the per-product accent), for which a commit message was proposed.
- Trevor's real settings are backed up in `artifacts\settings-backup-5.1` (the 0.4.0 file, no products answer) and were restored from it at the end of the session; restore from it after every on-screen run. The dev build is started with `dotnet run --project src\BingoHud.App\BingoHud.App.csproj` and asks the first-start question while the file has no answer.
- Running the dev build beside the installed 0.4.0: both share `%LOCALAPPDATA%\Bingo\settings.json`, and 0.4.0 drops the products answer when it saves its HUD position. Expect the question to return after touching the 0.4.0 HUD.
- The winget pull request for 0.4.0, microsoft/winget-pkgs#452730, was waiting on a moderator for its `Policy-Test-1.2` (Security) flag; see the installer feature's 5.4 note. The watcher that polled it ran in this session only. Task 5.8 added 2026-10-14 (Trevor's call) for the silent save failure seen once in 5.2; it runs after 5.6, before the checkpoint. Trevor's real settings are backed up in artifacts\settings-backup-5.1 and currently match it; restore from it after each on-screen run.

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
