# Installer and winget — Tasks

## Status Legend

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress
- `[P]` Parallelizable — executed concurrently as subagents, or inline when small
- `[C]` Checkpoint — stop and verify before continuing

Task ids are stable. Acceptance criteria from `spec.md` are cited per task.

The installer has no unit tests: it is a setup program, not code in Core. Test-first holds through
the check script (1.2), which is written before the installer, run red, and then decides whether
each installer task is done. The .NET suite is still run at every checkpoint, to show nothing in
the app moved.

Steps marked **(Trevor)** are public or outside the project, and are his to take or approve.

## Build and test commands

See the Building and Publishing sections of `README.md`. Every checkpoint runs `dotnet clean`
first, then `dotnet build` and `dotnet test`. The installer is built with Inno Setup's `ISCC.exe`,
as given in `plan.md` under Interfaces.

---

## Phase 1: Tooling and the check script

- [x] 1.1 Install Inno Setup 6 with `winget install JRSoftware.InnoSetup` **(Trevor approves:
      installs outside the project)**. Record the version, and where `ISCC.exe` landed, in the
      README's Building section.
- [x] 1.2 Write `installer/check-install.ps1`. Given `-Expect Installed -Version X.Y.Z
      [-StartsAtSignIn]` or `-Expect Uninstalled`, it prints one pass/fail line per check and exits
      non-zero on any failure. Checks:
      - the exe exists under `%LOCALAPPDATA%\Programs\Bingo` (or does not);
      - a Start menu shortcut named "Bingo" points at it (or is gone) (AC-3);
      - the Settings > Apps entry exists with name "Bingo", publisher `stevensT`, and the expected
        version (or is gone) (AC-4);
      - the Startup-folder shortcut is present exactly when `-StartsAtSignIn` is given, and gone
        after uninstall (AC-5, AC-8);
      - `%LOCALAPPDATA%\Bingo` exists after an uninstall that followed a use (AC-8);
      - no `BingoHud.App.exe` process is running from the install folder after an uninstall (AC-8).
- [x] 1.3 Run the check script on this machine with nothing installed: `-Expect Installed` must
      fail on every install check, and `-Expect Uninstalled` must pass. A check that cannot fail
      proves nothing.
- [x] 1.4 Checkpoint: clean, build, full suite; record in `progress.md`.

## Phase 2: The installer

- [x] 2.1 Confirm the source exe: hash `artifacts\self-contained\BingoHud.App.exe` against the
      published `Bingo-0.3.0-win-x64.exe` on the 0.3.0 release. If they differ, republish from
      the `v0.3.0` tag first. The setup must wrap the build that was released.
- [x] 2.2 Write `installer/bingo.iss` per the plan: fixed `AppId` (generated once, commented never
      to change), per-user, `{localappdata}\Programs\Bingo`, Start menu entry "Bingo", the
      unticked sign-in task, `CloseApplications=force`, the uninstall fallback, a ticked
      launch-at-finish box skipped when silent, version and source from the command line. Build
      `Bingo-0.3.0-setup.exe`.
- [x] 2.3 Fresh install, interactive, sign-in box left unticked; then `check-install.ps1 -Expect
      Installed -Version 0.3.0`. Launch from the Start menu and confirm the HUD appears. (AC-1,
      AC-2, AC-3, AC-4, AC-9)
- [x] 2.4 Uninstall from Settings > Apps with Bingo running; then `-Expect Uninstalled`. Record
      whether Restart Manager or the fallback closed it. (AC-8)
- [x] 2.5 Fresh install, interactive, sign-in box ticked; `-Expect Installed -StartsAtSignIn`.
      Sign out and in **(Trevor)**, and confirm Bingo starts. Uninstall and confirm the sign-in
      shortcut is gone. (AC-5, AC-8)
- [x] 2.6 Silent install with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`: no window appears,
      and `-Expect Installed` passes without `-StartsAtSignIn`. (AC-6)
- [x] 2.7 Upgrade: build a throwaway setup versioned 0.2.99 from the same exe, install it with the
      sign-in box ticked, move the HUD, leave Bingo running, then run the 0.3.0 setup silently.
      `-Expect Installed -Version 0.3.0 -StartsAtSignIn` passes, the HUD's position and settings
      are unchanged, and only one copy is installed. Delete the throwaway setup. (AC-7)
- [x] 2.8 Scan the setup file with Microsoft Defender
      (`MpCmdRun.exe -Scan -ScanType 3 -File …`). A detection stops here and is reported to
      Microsoft as a false positive before anything is uploaded.
- [x] 2.9 Checkpoint: clean, build, full suite; every installer criterion (AC-1 to AC-10) assessed;
      the trial HUD restored to how it was; record in `progress.md`.

## Phase 3: The release asset

- [x] 3.1 Upload `Bingo-0.3.0-setup.exe` to the existing 0.3.0 release, without touching the assets
      already there, and add a line about it to the release notes **(Trevor)**. (AC-1, AC-10)
      *Closed 2026-10-14 at Trevor's call with the upload done and the notes left unchanged: 0.3.0
      is never submitted to winget, and the 0.4.0 release carries the setup on its page. The 0.3.0
      notes still say there is no installer and end with a stray pasted fragment.*
- [x] 3.2 Download the uploaded setup from its public URL and hash it; the hash must equal the local
      file's. This is the hash the manifest uses.

## Phase 4: The manifest

- [x] 4.1 Write `packaging/winget/stevensT.Bingo/0.3.0/` — `stevensT.Bingo.yaml` (version),
      `stevensT.Bingo.locale.en-US.yaml` (defaultLocale), `stevensT.Bingo.installer.yaml`
      (installer: `inno`, `Scope: user`, the public URL, the 3.2 hash, `ProductCode` set to the
      AppId's registry key, `UpgradeBehavior: install`, `ReleaseNotesUrl`). Names, publisher and
      version match the installer exactly.
- [x] 4.2 `winget validate --manifest packaging\winget\stevensT.Bingo\0.3.0` passes. (AC-14)
- [x] 4.3 Turn on local manifests with `winget settings --enable LocalManifestFiles` **(Trevor
      approves: administrator setting)**. `winget install --manifest …`; `-Expect Installed`
      passes; `winget list Bingo` shows the entry `ARP\User\X64\{6B65FCD1-…}_is1` at 0.3.0.
      (AC-11, AC-14) *Amended 2026-10-08: winget links an install to a package ID only through a
      source, and `stevensT.Bingo` is in none until the pull request merges, so the by-ID check
      moved to 5.5.*
- [x] 4.4 Local upgrade: install the throwaway 0.2.99 setup, write a matching local 0.2.99 manifest
      under `artifacts\` pointing at that file, then confirm `winget upgrade --manifest` for 0.3.0
      replaces it and `winget list Bingo` moves the same entry from 0.2.99 to 0.3.0. Delete the
      throwaways. (AC-13, AC-14) *Amended 2026-10-08, as 4.3.*
- [x] 4.5 `winget uninstall Bingo`; `-Expect Uninstalled` passes. Turn local manifests back off if
      Trevor prefers. (AC-12, AC-14) *Amended 2026-10-08: by name, as 4.3; the package ID is in no
      source until the pull request merges.*
- [x] 4.6 Checkpoint: clean, build, full suite; AC-11 to AC-14 assessed; trial restored; record in
      `progress.md`.

## Phase 5: Submission and the routine

- [x] 5.1 Rewrite the README's release steps as one ordered checklist covering the whole path, from
      version bump to winget pull request, including the rule that a published asset is never
      replaced and how to uninstall by hand, settings folder included. Remove the Chocolatey
      mentions from the Publishing section. (AC-8, AC-16)
      *Amended 2026-10-14: the checklist includes writing the version's manifest folder under
      `packaging/winget/stevensT.Bingo/`, with the installer hash taken from the uploaded setup as
      3.2 did, and `winget validate` passing on it. Trevor's call, over a separate task, because
      every release repeats the step. The 0.4.0 folder is written by hand ahead of the checklist,
      since 5.4 needs it.*
- [x] 5.2 Add the installer and winget to `CHANGELOG.md` under `[Unreleased]`.
      *Done early, 2026-10-14, and in the 0.4.0 section rather than `[Unreleased]`: 0.4.0 is the
      first release to carry the setup on its page. It landed after the `v0.4.0` tag, which was
      already pushed and is not moved, so the tagged snapshot lacks the line. Winget is left out
      until the package is live (5.5); a line naming a package that cannot be installed yet
      would be wrong on the day it is read.*
- [x] 5.3 Run the review on the feature's changes and resolve or defer each finding, recording
      deferrals here.
      *Done 2026-10-14. Resolved: the winget description's click-through and notification claims;
      an executable version check in `check-install.ps1`; the release checklist's version check
      on both builds, folder-wide Defender scan with exit code, setup exit code, three-file hash
      list, and scripted comparison of the uploaded setup; `--scope user` for Inno Setup; comments
      that overstated how winget matches the install. Fixed but untested: `[InstallDelete]` in
      `bingo.iss`, so unticking "Start Bingo when I sign in" on a later run removes the shortcut.
      It ships with the next release. *Proven 2026-10-14 under 5.6, silently with `/TASKS=""`,
      red against the published 0.4.0 setup and green against the changed one.*
      Deferred, with Bingo running as an ordinary per-user app and the normal paths tested (2.4,
      2.7, 4.4, 4.5): a Bingo running elevated is invisible to the setup's and the check script's
      process match, so an uninstall can leave the exe behind; the uninstall fallback and the
      relaunch ignore their own results; a blocked PowerShell reads as "Bingo was not running";
      an install that aborts after Restart Manager closed Bingo does not start it again. Also
      deferred: an install moved with `/DIR=` (marked `deferred:` in `bingo.iss`); the check
      script does not look for a machine-wide entry or an entry found only by its `_is1` key, or
      confirm the uninstaller file exists. Rejected: "`Scope: user` untried through winget"; 4.3
      and 4.4 installed and upgraded through winget with the same manifest fields.*
- [~] 5.4 Submit: fork `microsoft/winget-pkgs`, copy the manifest to
      `manifests/s/stevensT/Bingo/0.4.0/`, open the pull request **(Trevor)**. Record the PR link
      here, and any pipeline findings and their fixes. *Amended 2026-10-13: 0.3.0 is never
      submitted. The 0.3.0 review fixes shipped as 0.4.0 before the submission, so the first
      version in winget is 0.4.0 rather than one already superseded.*
      *Opened 2026-10-14 17:07 UTC: https://github.com/microsoft/winget-pkgs/pull/452730, "New
      package: stevensT.Bingo version 0.4.0", adding only the three 0.4.0 manifest files. First
      label: Needs-CLA; Trevor agreed at 17:08, the label came off at 17:09 and `license/cla`
      passed. `New-Package` added by the validator at 17:10. At 17:56 the validator
      added `Azure-Pipeline-Passed` (the install on a clean machine passed) and `Policy-Test-1.2`,
      policy 1.2 being Security; the bot said the pull request needs manual review by a
      repository admin, and it was assigned to a moderator at 18:01. The validation guide does
      not list `Policy-Test-1.2` and the pull request gives no reason, so what triggered it is
      unknown. No author action was asked for.*
- [ ] 5.5 After the PR merges: on a machine or Windows Sandbox that has never had Bingo,
      `winget install stevensT.Bingo` with no local manifest, then `-Expect Installed`. (AC-15)
      Then, on this machine, `winget list stevensT.Bingo` shows the installed 0.4.0 by its package
      ID, which is the link `winget upgrade` depends on. (AC-13) *Added 2026-10-08, moved from 4.3.
      Amended 2026-10-13 to 0.4.0, as 5.4.*

- [x] 5.6 Make the setup close a running Bingo itself, so an install or upgrade run by winget does
      not depend on Restart Manager. *Added 2026-10-14, Trevor's call, after the winget install
      of 0.4.0 failed with Bingo running (progress, same date): run by winget, Restart Manager
      also reported `System` holding the exe and closed nothing. Not in the published 0.4.0
      setup; an upgrade runs the new version's setup, so this reaches everyone upgrading from
      0.4.0 when the next release ships.* (AC-7, AC-13)
      - Red is already recorded: the published 0.4.0 setup, run by `winget install --manifest`
        with Bingo running, exit code 5, twice.
      - In `bingo.iss`, end a Bingo running from the install folder in `PrepareToInstall`, before
        Restart Manager's check, with the uninstall fallback's existing process match.
        `BingoWasRunning` is still read in `InitializeSetup`, so the restart after install is
        unchanged.
      - Build the changed setup as 0.4.0 from the same executable into a throwaway folder under
        `artifacts\`, serve it from this machine, and write a throwaway local manifest under
        `artifacts\` pointing at it with its hash. With Bingo running,
        `winget install --manifest` on that manifest: exit code 0, Bingo running again as a new
        process, `check-install.ps1 -Expect Installed -Version 0.4.0 -StartsAtSignIn` passes.
      - Also run the changed setup directly with `/VERYSILENT` and Bingo running, and once with
        Bingo not running (Bingo must stay closed, as 2.6), so the direct paths still hold.
      - Add a winget-run install with Bingo running to the README's release checklist, since
        running the setup directly (step 9) did not catch this. It belongs in step 14, where the
        manifest exists: `winget install --manifest` reruns the setup even over the same version,
        as seen 2026-10-14, then the check script.
      - Restore: reinstall the published 0.4.0 setup, delete the throwaway folders, and Trevor
        turns local manifests off.
      *Done 2026-10-14; see progress. Reviewed (commit `5cb155d`): no defects. Resolved: step 14
      now says the checks alone cannot show winget ran the setup over the version step 9
      installed; the "Successfully installed" message and Bingo restarted as a new process do.
      Deferred: an install that stops between Preparing and the end (cancel, write error,
      rollback) leaves Bingo closed, as Restart Manager's close at the same stage already did
      before this change; relaunching from `DeinitializeSetup` would lift it. Also deferred: a
      Bingo started while the wizard is open is closed and not restarted, since the relaunch
      follows only what was running when setup began.*

## Build Verification

- [ ] BV.1 `dotnet clean` then `dotnet build`: 0 warnings, 0 errors.
- [ ] BV.2 `dotnet test`: full suite, no failures, no unexplained skips.
- [ ] BV.3 The installer builds from a clean checkout by following the README alone.
- [ ] BV.4 Final checkpoint: all green, every acceptance criterion met, recorded in `progress.md`.
