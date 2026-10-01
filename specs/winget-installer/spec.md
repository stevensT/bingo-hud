# Installer and winget

## Overview

Bingo ships today as a bare executable on the GitHub releases page. Getting it means finding the
repository, downloading a file, clicking through a SmartScreen warning, putting the file somewhere,
and remembering to launch it, because nothing puts it in the Start menu or starts it with Windows.
Removing it means knowing which folder it was left in.

This feature gives Bingo a proper installer and lists it in winget, the package manager built into
Windows. Someone can then run `winget install Bingo`, or download one setup file, and get Bingo in
the Start menu, in Settings > Apps for uninstalling, and optionally starting at sign-in. Upgrading
is `winget upgrade`.

The research that shaped this is recorded under Dependencies. The deciding fact: winget can list a
bare executable as a "portable" package, but a portable package gets no Start menu entry. That is
a known winget limitation, and it suits command-line tools, not a GUI that sits in the corner of
the screen, so an installer is the price of a good winget experience.

## User Stories

- As a Windows user who has heard of Bingo, I want to install it with one winget command, so that
  I do not have to find a download page or decide where to keep an executable.
- As a user, I want Bingo in the Start menu after installing, so that I can launch it the way I
  launch anything else.
- As a user who wants the HUD there every day, I want to choose at install time to have Bingo start
  when I sign in, so that I never have to remember to open it.
- As a user who no longer wants Bingo, I want to remove it from Settings > Apps or with winget, so
  that it leaves nothing running and nothing in the Start menu.
- As a user on an older version, I want `winget upgrade` to bring me the new one without losing my
  HUD position or settings.
- As the maintainer, I want a written checklist for each release, so that publishing a version to
  winget is a routine I can follow rather than something to rediscover.

## Acceptance Criteria

### The installer
- [ ] AC-1: Each release publishes a setup program, `Bingo-X.Y.Z-setup.exe`, as a release asset
      beside the two existing executables, which stay.
- [ ] AC-2: The installer installs for the current user only and never asks for administrator
      rights.
- [ ] AC-3: After installing, Bingo is in the Start menu under the name "Bingo", and choosing it
      starts the HUD.
- [ ] AC-4: Bingo appears in Settings > Apps > Installed apps as "Bingo", with its publisher and
      version, and can be uninstalled from there.
- [ ] AC-5: The installer offers "Start Bingo when I sign in", unticked by default. When ticked,
      Bingo starts at the next sign-in. Uninstalling removes it from sign-in.
- [ ] AC-6: The installer runs silently when asked to, with no windows or prompts, using the
      defaults: Start menu entry, not started at sign-in.
- [ ] AC-7: Installing over an earlier version closes a running Bingo first, replaces it, and keeps
      the user's settings, HUD position, alert state, and start-at-sign-in choice.
- [ ] AC-8: Uninstalling closes a running Bingo and removes its files, its Start menu entry, and its
      sign-in entry. The settings folder (`%LOCALAPPDATA%\Bingo`) is left in place, so reinstalling
      restores the HUD where it was, and the README says how to remove it by hand.
- [ ] AC-9: An installed Bingo runs on a machine with no .NET installed.
- [ ] AC-10: The installer is the same file whether it is reached through winget or downloaded from
      the releases page.

### winget
- [ ] AC-11: `winget install` with Bingo's package identifier installs it silently, and AC-3 and
      AC-4 hold afterwards.
- [ ] AC-12: `winget uninstall` removes it, and AC-8 holds afterwards.
- [ ] AC-13: With an older version installed, `winget upgrade` offers the newer one and AC-7 holds
      after upgrading. `winget list` shows the installed version correctly before and after.
- [ ] AC-14: The manifest is checked locally before it is submitted: it passes winget's own
      validation and installs, upgrades, and uninstalls from the local manifest on this machine.
- [ ] AC-15: The first version is accepted into the winget community repository and installs on a
      machine that has never had Bingo, by name, with no local manifest.

### The routine
- [ ] AC-16: The README's release steps cover the whole path, in order: version, changelog, tag,
      publish, build the installer, upload, hash, update the manifest, check it locally, submit.
      Following them alone is enough to release a new version to winget.

## Non-Goals

- **Chocolatey.** Planned since 0.1.0 and set aside: winget ships with Windows 11 and needs no
  extra account. Its own feature, if anyone asks for it.
- **Automated submission.** No workflow builds on each tag or opens the winget pull request. Each
  release is submitted by hand from the checklist until the process is familiar.
- **Code signing.** The installer and executables stay unsigned; SmartScreen will warn on a direct
  download, as it does today.
- **A machine-wide install**, MSIX, or the Microsoft Store.
- **A start-at-sign-in setting inside the app.** The installer's checkbox is the only switch in this
  feature; a tray setting that works however Bingo was installed is its own feature.
- **Removing settings on uninstall**, or asking whether to.
- **Auto-update.** winget upgrades when the user asks it to; Bingo does not update itself (a
  quota-hud non-goal that stands).
- **32-bit or ARM64 builds.**
- **Any change to the app itself.** If the installer needs the app to behave differently, that is
  a finding to raise, not something to build here.

## Open Questions

None open. Decided 2026-10-01, recorded so they are not reopened:
- **Publisher and identifier:** the publisher is `stevensT`, Trevor's GitHub name, and the winget
  package identifier is `stevensT.Bingo`. Permanent once the first version is accepted.
- **First version:** 0.3.0. The app is unchanged by this feature, so the installer wraps the
  existing 0.3.0 self-contained executable and is added to the published 0.3.0 release as a new
  asset. The assets already there are not touched.

## Dependencies

- **The GitHub releases page** — the installer's download URL lives there, and winget's manifest
  points at it with a SHA-256 hash. A release asset must never be replaced once a manifest points
  at it; a changed file is a new version.
- **An installer-building tool on the development machine.** Choosing it is the plan's job; it must
  produce a per-user installer with a silent mode that winget recognizes.
- **The winget community repository** (`microsoft/winget-pkgs`), submitted to by pull request from
  Trevor's GitHub account. Each submission is validated by Microsoft's pipeline: manifest checks,
  URL and hash checks, a Defender scan, and an install in a sandbox. The first version of a new
  package also waits for a human moderator, which can take from hours to days. Unsigned installers
  are accepted, but a Defender false positive stops a submission until it is reported and cleared.
- **Research, 2026-10-01.** winget's `portable` installer type adds a command alias but no Start
  menu shortcut (an open request on winget-cli), which ruled it out for a GUI app. "Bingo" is not
  taken in winget (`winget search bingo` returns nothing) or Chocolatey.
