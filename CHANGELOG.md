# Changelog

All notable changes to Bingo are recorded here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Bingo stays on `0.x` until the app has run reliably through real daily use. Because it reads
an undocumented upstream endpoint whose payload has drifted before, anything below `1.0.0` should
be treated as liable to break when that endpoint changes.

## [Unreleased]

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

### Added
- Capture script for the usage endpoint, writing dated, scrubbed fixtures. First recorded
  fixtures: a successful read and an authentication failure.
- MIT license.
- README covering what Bingo is, how it reads quota, requirements, architecture, and the
  versioning policy.
- Project structure: constitution, quota HUD spec and technical plan.
- Research teardown of three prior-art Claude usage monitors.
