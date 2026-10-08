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
- [ ] 2.8 Scan the setup file with Microsoft Defender
      (`MpCmdRun.exe -Scan -ScanType 3 -File …`). A detection stops here and is reported to
      Microsoft as a false positive before anything is uploaded.
- [ ] 2.9 Checkpoint: clean, build, full suite; every installer criterion (AC-1 to AC-10) assessed;
      the trial HUD restored to how it was; record in `progress.md`.

## Phase 3: The release asset

- [ ] 3.1 Upload `Bingo-0.3.0-setup.exe` to the existing 0.3.0 release, without touching the assets
      already there, and add a line about it to the release notes **(Trevor)**. (AC-1, AC-10)
- [ ] 3.2 Download the uploaded setup from its public URL and hash it; the hash must equal the local
      file's. This is the hash the manifest uses.

## Phase 4: The manifest

- [ ] 4.1 Write `packaging/winget/stevensT.Bingo/0.3.0/` — `stevensT.Bingo.yaml` (version),
      `stevensT.Bingo.locale.en-US.yaml` (defaultLocale), `stevensT.Bingo.installer.yaml`
      (installer: `inno`, `Scope: user`, the public URL, the 3.2 hash, `ProductCode` set to the
      AppId's registry key, `UpgradeBehavior: install`, `ReleaseNotesUrl`). Names, publisher and
      version match the installer exactly.
- [ ] 4.2 `winget validate --manifest packaging\winget\stevensT.Bingo\0.3.0` passes. (AC-14)
- [ ] 4.3 Turn on local manifests with `winget settings --enable LocalManifestFiles` **(Trevor
      approves: administrator setting)**. `winget install --manifest …`; `-Expect Installed`
      passes; `winget list stevensT.Bingo` shows 0.3.0. (AC-11, AC-14)
- [ ] 4.4 Local upgrade: install the throwaway 0.2.99 setup, write a matching local 0.2.99 manifest
      in the scratchpad pointing at that file, then confirm `winget upgrade --manifest` for 0.3.0
      replaces it and `winget list` moves from 0.2.99 to 0.3.0. Delete the throwaways. (AC-13, AC-14)
- [ ] 4.5 `winget uninstall stevensT.Bingo`; `-Expect Uninstalled` passes. Turn local manifests back
      off if Trevor prefers. (AC-12, AC-14)
- [ ] 4.6 Checkpoint: clean, build, full suite; AC-11 to AC-14 assessed; trial restored; record in
      `progress.md`.

## Phase 5: Submission and the routine

- [ ] 5.1 Rewrite the README's release steps as one ordered checklist covering the whole path, from
      version bump to winget pull request, including the rule that a published asset is never
      replaced and how to uninstall by hand, settings folder included. Remove the Chocolatey
      mentions from the Publishing section. (AC-8, AC-16)
- [ ] 5.2 Add the installer and winget to `CHANGELOG.md` under `[Unreleased]`.
- [ ] 5.3 Run the review on the feature's changes and resolve or defer each finding, recording
      deferrals here.
- [ ] 5.4 Submit: fork `microsoft/winget-pkgs`, copy the manifest to
      `manifests/s/stevensT/Bingo/0.3.0/`, open the pull request **(Trevor)**. Record the PR link
      here, and any pipeline findings and their fixes.
- [ ] 5.5 After the PR merges: on a machine or Windows Sandbox that has never had Bingo,
      `winget install stevensT.Bingo` with no local manifest, then `-Expect Installed`. (AC-15)

## Build Verification

- [ ] BV.1 `dotnet clean` then `dotnet build`: 0 warnings, 0 errors.
- [ ] BV.2 `dotnet test`: full suite, no failures, no unexplained skips.
- [ ] BV.3 The installer builds from a clean checkout by following the README alone.
- [ ] BV.4 Final checkpoint: all green, every acceptance criterion met, recorded in `progress.md`.
