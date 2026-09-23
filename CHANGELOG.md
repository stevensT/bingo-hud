# Changelog

All notable changes to Bingo are recorded here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Bingo stays on `0.x` until the app has run reliably through real daily use. Because it reads
an undocumented upstream endpoint whose payload has drifted before, anything below `1.0.0` should
be treated as liable to break when that endpoint changes.

## [Unreleased]

## [0.1.0] - 2026-09-22

First release: a Windows HUD that shows Claude Code's session and weekly usage limits.

### Added
- Always-on-top HUD showing the 5-hour and weekly windows, each with its percentage, its
  direction ("used" or "left"), and when it resets.
- Click-through by default; resting the cursor on the HUD briefly makes it clickable, so it can
  be dragged. It snaps to screen edges and remembers where it was left.
- Severity on the HUD: a window's figure turns amber at 25% remaining and red at 10%, magenta
  with the word "limited" when the server is refusing work, and a bar on the left edge shows the
  worst of them. A reading that can no longer update is drawn without colour.
- Optional collapse to the single worst window, unless both need attention.
- Detail panel, opened by clicking the HUD: exact reset times, per-model weekly caps where the
  account has any, the reading's age, the app version, and a manual refresh that says why and
  when it will next be allowed if it is refused.
- Tray icon with collapse, display direction, mute, open panel, and quit.
- Desktop notifications at 25% and 10% remaining, at most once per window, surviving restarts and
  rearming when the window resets. Crossings that land together arrive as one notification.
- Adaptive polling between 2 and 30 minutes, faster while Claude Code is writing transcripts,
  backing off on rate limits and honouring `Retry-After`.
- A reading that is no longer current stays on screen marked with its age, and with the reason
  when it cannot update. Sign-in, permission, unreadable, unavailable, and unsupported states each
  say what happened and what to do.
- Reads the token Claude Code already keeps in `~/.claude/.credentials.json`. Bingo never
  refreshes or writes it.
- Two builds, each a single executable: a self-contained one that needs nothing installed, and
  a framework-dependent one for package managers, which needs the .NET 9 Desktop Runtime.
- Capture script for the usage endpoint, writing dated, scrubbed fixtures. First recorded
  fixtures: a successful read and an authentication failure.
- MIT license.
- README covering what Bingo is, how it reads quota, requirements, architecture, and the
  versioning policy.
- Project structure: constitution, quota HUD spec and technical plan.
- Research teardown of three prior-art Claude usage monitors.

### Changed
- Percentages now read as consumed rather than remaining, matching what `/usage` reports.
  The direction is a setting, and the figure states which direction it is in so it cannot be
  read backwards.
- Technical plan revised against a live response. The endpoint no longer returns a top-level
  `status` field; windows and their severities now arrive in a `limits` array, which becomes the
  primary source with the older flat keys kept as a fallback.
- Renamed the project to Bingo. Assemblies and namespaces become `BingoHud.Core`
  and `BingoHud.App`. The "Quota HUD" feature name and its `specs/quota-hud/` path are unchanged —
  that is the feature, not the product.
