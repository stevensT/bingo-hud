# Installer and winget — Progress

updated: 2026-10-08
status: Phase 1 in progress; tasks 1.1 to 1.3 done
blockers: none
next_session: start at task 1.4, the Phase 1 checkpoint. `ISCC.exe` is not on `PATH`; it is at
`%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`, so the build command in `plan.md` (Interfaces)
needs the full path. See "Where the 2026-10-01 session stopped" below.

## 2026-10-08: the check script run red (1.3)

- On this machine with nothing installed, `-Expect Installed` failed on the executable, the Start
  menu shortcut and the Settings > Apps entry, and `-Expect Uninstalled` passed all six checks.
- That run cannot reach the checks that depend on something existing: the shortcut targets and
  the entry's name, publisher and version. So a deliberately wrong install was planted and then
  removed (Trevor approved): a renamed copy of `ping.exe` as the executable, kept running, Start
  menu and Startup shortcuts pointing at Notepad, and an entry named "Bingo version 0.2.0" by
  "someone". Every dependent check failed against it, every Uninstalled check except the kept
  settings folder failed, and all passed again once it was removed.
- The planted run found a bug: with exactly one Settings > Apps entry, Windows PowerShell 5.1
  reported the count as blank and skipped the name, publisher and version checks. A one-item
  result comes back from a function as a single object, and a single registry entry has no
  `.Count` in 5.1. Fixed by calling each query as `@(Get-...)`.
- Not exercised: the "exactly one" checks with two or more matches. Those use the same count as
  the checks above, now that the count is fixed.
- The entry-name check is strict on purpose. Inno Setup names the entry "Bingo version X.Y.Z"
  unless told otherwise, so `bingo.iss` (2.2) must set `UninstallDisplayName=Bingo`.

## Where the 2026-10-01 session stopped

- **Released:** v0.1.0 (`d08d170`), v0.2.0 (`f1de9f2`) and v0.3.0 (`e8c2c61`) are tagged and
  pushed. Only 0.3.0 has a GitHub release, marked Pre-release, with
  `Bingo-0.3.0-win-x64.exe` (SHA-256 `5B4F2D9C…3FE8`) and
  `Bingo-0.3.0-win-x64-framework-dependent.exe` (`D0F52127…C2D1`). Task 2.1 hashes the local
  self-contained exe against the first of those before wrapping it.
- **The display-options feature is complete** (`specs/hud-display-options/`), including the 0.3.0
  amendment that made the normal bar Claude orange.
- **The trial HUD** runs from `src/BingoHud.App/bin/Release/net9.0-windows/win-x64/`, now a 0.3.0
  build, with Trevor's own settings in `%LOCALAPPDATA%\Bingo`. Any on-screen test stops it, backs
  that folder up, and restores both afterwards.
- **The test stub server is not in the repository.** It lives at `f5027f7:scripts/stub-usage-server.js`
  and was last run from the session scratchpad; restore it from history if a forced state is
  needed again.
- **Spelling:** American in anything a user sees (UI, README, CHANGELOG, release notes); internal
  comments, tests and specs are left as written.
- **Open idea, not scheduled:** the toast sender reads "BingoHud.App". The installer's Start menu
  shortcut could carry an app identity that fixes it, but that touches the app and is a non-goal
  of this feature.

## Checkpoints
