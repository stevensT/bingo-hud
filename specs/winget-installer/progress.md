# Installer and winget — Progress

updated: 2026-10-08
status: Phase 2 complete; Phase 3 not started
blockers: none
next_session: start at task 3.1, Trevor uploading `artifacts\release-0.3.0\Bingo-0.3.0-setup.exe`
(SHA-256 `9B7809BB...FD5FD5`) to the 0.3.0 release. Bingo 0.3.0 is installed, starts at sign-in,
and is Trevor's daily copy now, in place of the trial HUD.

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

## 2026-10-08: the first install (2.3)

- Installed interactively by Trevor, sign-in box unticked. No administrator prompt (AC-2). No
  SmartScreen warning either, because the setup was built on this machine and carries no
  downloaded-file mark; a download from the release will still warn, as the spec expects.
- `check-install.ps1 -Expect Installed -Version 0.3.0`: all eight checks pass (AC-3, AC-4 and the
  unticked half of AC-5). The Settings > Apps key is `{6B65FCD1-588C-4977-8EEB-6D3A62AD659F}_is1`
  and its quiet uninstall string is `unins000.exe /SILENT`.
- Bingo started seven seconds after setup finished, likely from the launch box, so it was stopped
  and started again by opening the Start menu shortcut itself: it ran from the install folder.
  Trevor confirmed the HUD on screen (AC-3).
- The trial HUD was not running. The settings folder was backed up first; the installed Bingo uses
  the same folder, as it should.
- AC-9 (runs with no .NET) cannot be shown here, since this machine has the .NET SDK. It holds by
  construction, the executable being self-contained, and is confirmed on a clean machine at 5.5.

## 2026-10-08: uninstalling with Bingo running (2.4)

- Trevor uninstalled from Settings > Apps with Bingo running from the Start menu shortcut. One
  confirmation prompt, no error about files in use.
- `check-install.ps1 -Expect Uninstalled`: all six checks pass. The install folder is gone
  entirely; `%LOCALAPPDATA%\Bingo` is kept (AC-8).
- The fallback in `bingo.iss`'s code section closed Bingo, not Restart Manager. The Application
  log's Restart Manager events show one session, 11:03:28 to 11:03:47, which is the install; the
  uninstall opened none. The comment in the script was corrected to say so.

## 2026-10-08: the sign-in install (2.5)

- Trevor installed interactively with "Start Bingo when I sign in" ticked.
  `check-install.ps1 -Expect Installed -Version 0.3.0 -StartsAtSignIn`: all nine checks pass,
  and the Startup shortcut points at the installed executable.
- The settings backup was copied from the session scratchpad to `artifacts\settings-backup-2.3`
  before signing out, since signing out ends the session.
- Trevor signed out and in. Explorer started at 11:18:15 and Bingo, from the install folder, at
  11:19:26 with no hand from Trevor; the gap is Windows' usual delay for Startup-folder apps
  (AC-5).
- Uninstalled with Bingo running, this time silently through the entry's own uninstaller
  (`unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`), exit code 0.
  `-Expect Uninstalled`: all six checks pass, the sign-in shortcut among them (AC-5, AC-8).

## 2026-10-08: the silent install (2.6)

- `Bingo-0.3.0-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`: exit code 0, and Trevor
  saw no window, progress bar, or prompt (AC-6).
- `-Expect Installed -Version 0.3.0`: all eight checks pass, with no sign-in shortcut. The ticked
  choice from 2.5 did not come back: uninstalling removed the entry Inno Setup remembers it in.
- Bingo was not started, so the launch box is skipped when silent, as intended.

## 2026-10-08: the upgrade (2.7)

- A throwaway 0.2.99 setup was built from the same executable and installed silently with
  `/TASKS="startatsignin"`. Bingo was started from the Start menu and Trevor moved the HUD. The
  0.3.0 setup then ran silently over it: exit code 0, all nine checks pass with `-StartsAtSignIn`,
  one Settings > Apps entry now at 0.3.0, and `alerts.json` and `settings.json` byte for byte
  unchanged. Trevor confirmed the HUD came back where Trevor had put it (AC-7).
- **Finding: Bingo did not come back after the upgrade.** Restart Manager closed it (event 10002,
  "Shutting down application or service 'BingoHud.App'") but `RestartApplications=yes` did not
  reopen it, because Restart Manager only restarts apps that register for restart and Bingo does
  not. A `winget upgrade` would have left the HUD gone until the next sign-in.
- **Fixed in the installer, Trevor's choice** over accepting it or deferring to an app change.
  `bingo.iss` now checks at startup whether Bingo is running from the install folder and, if so,
  starts it again after the files are replaced, silent or not. The "Launch Bingo" box is hidden in
  that case, since Bingo has no single-instance guard and the box would start a second copy.
  `RestartApplications` is now `no`, so Bingo registering for restart one day cannot double it.
  The process lookup is shared with the uninstall fallback.
- Rerun after the fix: silent upgrade from a running 0.2.99 left exactly one Bingo, a new process
  seven seconds later; all checks pass and the settings are unchanged. A silent install with Bingo
  not running still leaves it closed (2.6 holds). Two interactive reinstalls over a running 0.3.0
  each left exactly one new Bingo, and Trevor saw no "Launch Bingo" box on either.
- The throwaway 0.2.99 setup was built in the session scratchpad, which the session lost before it
  could be deleted; it was never in the project. 4.4 rebuilds one.

## 2026-10-08: the Defender scan (2.8)

- Scanned the setup as rebuilt in 2.7, `Bingo-0.3.0-setup.exe`, 69,114,465 bytes, SHA-256
  `9B7809BB9DF229311955A48FB7F58152960CA047B20A58E4F3948A8107FD5FD5`, and the installed
  `BingoHud.App.exe`, with `MpCmdRun.exe -Scan -ScanType 3 -File ... -DisableRemediation`.
  Both: "found no threats", exit code 0.
- Defender platform 4.18.26080.4, signatures 1.459.601.0 from 2026-10-07. Trevor approved a
  signature update before closing: Defender reported no updates needed, so 1.459.601.0 was already
  current, and a rescan of both files was clean again.

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

### CP: Phase 2 The installer — 2026-10-08
tests: 1455 pass / 0 fail / 0 skip (unchanged; the app is untouched)
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8
rework: 2.7 found that Restart Manager does not reopen Bingo after an upgrade; `bingo.iss` gained a
relaunch (Trevor's choice), and 2.6 and 2.7 were rerun against the rebuilt setup. 2.4 found the
uninstaller does not use Restart Manager at all; the code-section fallback is what closes Bingo,
and the script's comment says so.
criteria_met:
- AC-2 met: no administrator prompt (2.3); per-user entry under HKCU.
- AC-3 met: Start menu entry "Bingo" opens the HUD (2.3).
- AC-4 met: Settings > Apps shows Bingo, stevensT, 0.3.0, and uninstalls from there (2.3, 2.4).
- AC-5 met: unticked by default (2.3, 2.6); ticked starts Bingo at sign-in (2.5); uninstall removes
  it (2.5).
- AC-6 met: silent install, no window, defaults applied (2.6).
- AC-7 met: upgrade over a running Bingo closes it, replaces it, keeps settings, position, alert
  state and the sign-in choice, and now reopens it (2.7).
- AC-8 partly met: uninstall closes Bingo and removes files, Start menu and sign-in entries, and
  keeps the settings folder (2.4, 2.5). The README's removal-by-hand note is 5.1.
- AC-9 by construction only: the executable is self-contained, but this machine has the .NET SDK.
  Confirmed on a clean machine at 5.5.
- AC-1 and AC-10 open: the setup is built but not yet on the release (3.1, 3.2).
issues:
- Phase 2 was run inline: every task needed Trevor at the screen or depended on the one before.
- Trial HUD not restored, by Trevor's choice: the installed 0.3.0 replaces it as the daily copy,
  with the current settings kept (the HUD position moved in 2.7). `artifacts\settings-backup-2.3`
  is no longer needed. The `bin\Release` build is still on disk; running it alongside the installed
  copy would show two HUDs on the same settings.
