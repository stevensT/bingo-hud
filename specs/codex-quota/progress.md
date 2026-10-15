# Codex Quota — Progress

updated: 2026-10-14
status: Phase 2 in progress; 2.1 and 2.2 done 2026-10-14
blockers: none
next_session: task 2.3, the Codex monitor's tests: rescan only on a moved write time, idle vs failed, age from the record.

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
