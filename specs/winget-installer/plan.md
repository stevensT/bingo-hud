# Installer and winget — Technical Plan

## Technical Approach

Three pieces, none of which touches the app:

1. **An Inno Setup script** (`installer/bingo.iss`) that wraps the self-contained executable into
   `Bingo-X.Y.Z-setup.exe`. Per-user, Start menu entry, an optional sign-in shortcut, and an entry
   in Settings > Apps. The version and the path to the executable are passed in on the command line,
   so the script holds no version of its own and cannot drift from the csproj.
2. **A winget manifest** (`packaging/winget/stevensT.Bingo/X.Y.Z/`, three YAML files) that points at
   the setup file on the GitHub release with its SHA-256. The repo copy is the source of truth; a
   submission copies it into a fork of `microsoft/winget-pkgs`.
3. **A check script** (`installer/check-install.ps1`) that inspects the machine after an install,
   upgrade, or uninstall and reports each acceptance criterion as pass or fail: the Start menu and
   sign-in shortcuts, the Settings > Apps entry and its version, the installed files, the settings
   folder, and whether Bingo is running. It is written before the Inno script, run red against a
   machine with nothing installed, and is how every installer task is verified.

```
dotnet publish (self-contained)  ──►  artifacts/self-contained/BingoHud.App.exe
        │
ISCC /DAppVersion=X.Y.Z /DSource=…  installer/bingo.iss
        │
        ▼
artifacts/release-X.Y.Z/Bingo-X.Y.Z-setup.exe  ──► uploaded to the GitHub release
        │                                                   │
   Get-FileHash ──► packaging/winget/…/installer.yaml ◄─────┘ (InstallerUrl)
        │
winget validate ─► winget install --manifest (local) ─► check-install.ps1 ─► PR to winget-pkgs
```

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Installer tool | Inno Setup 6 | Free, a single text script, per-user installs without admin, and winget knows it: `InstallerType: inno` supplies the silent switches itself, which is AC-6 for free. MSIX was ruled out because it must be signed; WiX builds an MSI, which is heavier to author for no gain here. |
| Install location | `{localappdata}\Programs\Bingo` with `PrivilegesRequired=lowest` | Windows' standard per-user program folder; no admin prompt (AC-2). |
| Upgrade identity | A fixed `AppId` GUID, generated once and never changed | Inno uses it to find the previous install and replace it in place (AC-7). Its registry key, `{GUID}_is1`, is what winget matches to the installed app; it goes into the manifest as `ProductCode` so `winget list` and `winget upgrade` correlate (AC-13). |
| Name, publisher, version in Settings > Apps | `AppName=Bingo`, `AppPublisher=stevensT`, `AppVersion` from the command line | winget matches its `PackageName`, `Publisher`, and `PackageVersion` against these, so they are the same strings in both places. |
| Start-at-sign-in | An Inno *task*, unticked, that adds a shortcut to the user's Startup folder | Unticked by default meets AC-5, and a silent install skips unticked tasks, so winget gets the default (AC-6). A Startup-folder shortcut is listed under the installer's own files, so uninstall removes it with no extra code (AC-8). Inno remembers the choice and keeps it on upgrade (AC-7). |
| Closing a running Bingo | `CloseApplications=force`, plus an uninstall step that ends the process if it is still running | Bingo has no single-instance guard, so the installer finds it by the file it holds open, through Windows' Restart Manager. `force` closes it even in silent mode instead of failing on a locked file. The uninstall fallback exists because Restart Manager support in the uninstaller is the least certain part; the check script confirms which path actually closed it. |
| Relaunch after install | A ticked "Launch Bingo" box on the final page; skipped when silent | The usual Windows convention. A winget install leaves the user to start it from the Start menu, which is what AC-11 checks. |
| Settings on uninstall | Left in `%LOCALAPPDATA%\Bingo` | Spec AC-8. Inno only removes what it installed, so this needs no code. |
| Executable name | Installed as `BingoHud.App.exe`, unchanged | Renaming the file could change how Windows identifies the app's notifications, which is an app change (a non-goal). The Start menu entry is named "Bingo", which is what a user sees. |
| Manifest format | Multi-file, schema 1.12.0: version, defaultLocale, installer | Required by winget-pkgs for new submissions. `Scope: user`, `InstallerType: inno`, `UpgradeBehavior: install`, `ReleaseNotesUrl` pointing at the GitHub release. |
| Manifest authoring | Written by hand into the repo, checked with `winget validate` | Three short files; writing them teaches what each field does, and the repo copy makes the next version a copy-and-edit. `wingetcreate` stays an option for later automation. |
| Local install test | `winget install --manifest` against the repo copy | The only way to prove the manifest installs before Microsoft's pipeline does. It needs `LocalManifestFiles` turned on in winget, an administrator setting on this machine; asked for at that task. |
| First version | 0.3.0, setup added to the existing release | Decided in the spec. The setup wraps the 0.3.0 executable already published, and its SHA-256 is checked against the release asset before wrapping. |
| Changelog | A line under `[Unreleased]` for the installer and winget | The changelog records versions of the app. The installer is a new way to get it, recorded when it lands and carried into the next version's section. |

## Data Model

None. The feature adds files, not types.

## Interfaces

- **Building the installer:**
  `ISCC.exe /DAppVersion=0.3.0 /DSourceExe=artifacts\self-contained\BingoHud.App.exe /DOutputDir=artifacts\release-0.3.0 installer\bingo.iss`
  → `artifacts\release-0.3.0\Bingo-0.3.0-setup.exe`
- **Silent install** (what winget runs for `inno`): `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`
- **Check script:** `installer\check-install.ps1 -Expect Installed -Version 0.3.0 [-StartsAtSignIn]`,
  `-Expect Uninstalled`. Exits non-zero if any check fails, and prints one line per check.
- **winget package:** `stevensT.Bingo`, manifests under `manifests/s/stevensT/Bingo/0.3.0/` in
  `microsoft/winget-pkgs`.

## Implementation Phases

1. **Tooling and the check script** — install Inno Setup (asks first: it installs outside the
   project), write the check script, run it red.
2. **The installer** — the Inno script, built against the 0.3.0 executable; install, sign-in task,
   upgrade (from a throwaway older-versioned build of the same executable), and uninstall, each
   verified by the check script. A Defender scan of the setup file.
3. **The release asset** — upload the setup to the 0.3.0 release (Trevor's step), then hash the
   uploaded file, not the local one.
4. **The manifest** — write it, `winget validate`, local install, upgrade and uninstall through
   winget, each verified by the check script.
5. **Submission and the routine** — README release checklist and changelog line; Trevor submits
   the pull request; the feature closes when the package installs by name on a machine that never
   had it.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Defender or the winget sandbox flags the unsigned setup file | The submission stalls until a false-positive report clears it, days at worst | Scan locally with Defender before uploading. If flagged, report it to Microsoft before submitting, not after. |
| Restart Manager does not close the tray app, so upgrade or uninstall fails on a locked file | Upgrades break for anyone with Bingo running, which is everyone | `CloseApplications=force` plus the uninstall fallback; the upgrade test runs with Bingo open on purpose, and the check script confirms it closed and came back. |
| winget does not correlate the installed app with the package, so `winget upgrade` never offers anything | Users stay on old versions forever, silently | `ProductCode` set to the Inno registry key; names and versions match exactly; AC-13 is tested locally with two versions before submitting. |
| A release asset is replaced after the manifest points at it | Every winget install fails its hash check | Written into the release checklist: a published asset is never replaced; a changed file is a new version. |
| The `AppId` changes between versions | Upgrades install a second copy beside the first | Generated once, committed in the script with a comment saying never to change it. |
| Moderation of a first-time package takes days | The feature cannot close the day it is submitted | Expected, not a defect. The feature's last task waits on it and nothing else does. |
| The setup file wraps a different executable than the release's own 0.3.0 asset | The installer would ship a build nobody tested | The plan hashes the source executable against the published `Bingo-0.3.0-win-x64.exe` before building. |
