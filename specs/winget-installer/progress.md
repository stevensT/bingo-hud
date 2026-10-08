# Installer and winget — Progress

updated: 2026-10-08
status: Phase 2 in progress; tasks 2.1 and 2.2 done
blockers: none
next_session: start at task 2.3, the first interactive install. The setup is built at
`artifacts\release-0.3.0\Bingo-0.3.0-setup.exe`; rebuild it with the command at the top of
`installer\bingo.iss`, run from the repository root.

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

## 2026-10-08: the source executable confirmed (2.1)

- `artifacts\self-contained\BingoHud.App.exe`, the release's `Bingo-0.3.0-win-x64.exe` as listed,
  and the same asset downloaded fresh all hash to SHA-256
  `5B4F2D9C551C7CAF048FEE885283075599F4BA46E2BCA879663B743BB7863FE8`.
  The local file reports product version `0.3.0+e8c2c619c79f`, the `v0.3.0` tag. No rebuild was
  needed, so whether a rebuild from the tag reproduces the same bytes was never tested.

## 2026-10-08: the installer script (2.2)

- `installer/bingo.iss` written. AppId `6B65FCD1-588C-4977-8EEB-6D3A62AD659F`, so the Settings >
  Apps key and the manifest's ProductCode are `{6B65FCD1-588C-4977-8EEB-6D3A62AD659F}_is1`.
- `SourceDir=..`: Inno Setup resolves relative paths from the script's folder, which would have
  sent the plan's `/DSourceExe=artifacts\...` to `installer\artifacts\`. With the source folder set
  to the repository root, the plan's command works as written, given ISCC's full path.
- Choices the plan left open: the Start menu shortcut sits directly in Programs rather than in a
  folder; there is no folder page, since the location is fixed; no license page; `OutputDir`
  defaults to `artifacts` so a forgotten argument cannot drop a setup into the repository.
- The uninstall fallback ends only a Bingo running from the install folder, matched on full path,
  so the trial HUD in `bin\Release` is never touched by an uninstall test.
- Built with Inno Setup 6.7.3: no warnings. `Bingo-0.3.0-setup.exe` is 69,114,142 bytes; its
  version info reads Bingo, 0.3.0, stevensT. Local SHA-256 `35D6F097...F844C`, for reference only:
  the manifest uses the hash of the uploaded file (3.2).

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

### CP: Phase 1 Tooling and the check script — 2026-10-08
tests: 1455 pass / 0 fail / 0 skip (1455 at the 0.3.0 checkpoint; the app is untouched)
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 1.1, 1.2, 1.3
rework: 1.2's script fixed during 1.3, when the planted install showed one Settings > Apps entry
counted as blank under Windows PowerShell 5.1
criteria_met: none yet, as planned. Phase 1 builds the means of checking AC-3, AC-4, AC-5 and
AC-8; the criteria themselves are met in Phase 2 against the real installer.
issues:
- Phase 1 was run inline: three sequential tasks, none parallel.
- `ISCC.exe` is not on `PATH`; 2.2 calls it by its full path.
