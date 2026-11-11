# Scoped Cap Shape — Tasks

## Status Legend

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress
- `[P]` Parallelizable — run concurrently, or inline when small
- `[C]` Checkpoint — stop and verify before continuing

Task ids are stable. Each task cites the criteria it closes. Every test-and-implement task writes
its test first and watches it fail before the fix exists.

## Build and test commands

See the Building section of `README.md`. Every checkpoint runs `dotnet clean` first, then
`dotnet build` and `dotnet test`.

---

## Phase 1: Parsing

These run in sequence: all three touch `UsageNormalizer` or its tests.

- [x] 1.1 Test + implement: in `UsageNormalizerScopedCapTests`, rewrite the string-scope cases to
      the object form. A scope `{"model":{"id":null,"display_name":"Fable"},"surface":null}` gives
      a `WeeklyScoped` window labelled `Fable`, whatever its `kind`. `display_name` null or `""`
      with `id` `"claude-fable-5"` is labelled by the id. Extra keys inside `scope` and `model`
      are ignored. `scope` absent or null is still no scope. Fix: `ReadScope` reads the object.
      (AC-1, AC-2, AC-3)
- [x] 1.2 Test + implement: in `UsageNormalizerUnreadableTests`, each of these is unreadable with
      the reason the plan names: a string scope, a number scope, an array scope; `model` absent,
      null, a string; `model` with neither `display_name` nor `id` usable; `surface` set beside a
      valid `model`; `surface` set with `model` null. Replace the old "not a string" test. (AC-4)
- [x] 1.3 Test + implement: commit the 2026-11-11 capture as
      `tests/fixtures/usage/2026-11-11-scoped-cap.json` and `.meta.json`, add its inventory row,
      and add it to `Fixtures`. A contract test pins its `limits` entries (kinds, groups, and the
      scope's keys and value kinds), and a parser test reads it to a session window, a weekly
      window, and a cap labelled `Fable`. (AC-5)
- [x] 1.4 Checkpoint: clean, build, full suite; audit marks; AC-1 to AC-5; record in
      `progress.md`.

## Phase 2: Script and docs

- [x] 2.1 Fix `scripts/capture-usage.js` so the token comes from `claudeAiOauth` when that object
      exists, falling back to the recursive search only when it does not. Test-first is
      suspended: the script has no test harness and talks to the live endpoint (the precedent is
      audit-fixes 4.1). Verify by running it against the live endpoint with the raw output in a
      scratch folder, and confirming it reports `claudeAiOauth.accessToken` and a 200. (AC-6)
- [x] 2.2 Update the descriptions of `scope`: the doc comments on `QuotaWindow.Scope`,
      `WindowKind.WeeklyScoped`, `WindowName`, `PanelContent`, and `PanelReadout`, the
      `UsageNormalizerScopedCapTests` class summary, the per-model row in
      `specs/quota-hud/plan.md`, and a Fixed entry under Unreleased in `CHANGELOG.md`. Grep for
      `scope string` and `never yet` afterwards to confirm none remain. (AC-7)
- [x] 2.3 Checkpoint: clean, build, full suite; audit marks; all acceptance criteria; record in
      `progress.md`.

## Build Verification

- [x] `dotnet clean`, then `dotnet build` with zero warnings
- [x] `dotnet test`, all passing
- [ ] Run the app against the live endpoint and confirm the HUD shows the 5-hour and weekly
      readings and the panel lists the Fable cap
- [ ] Final checkpoint recorded in `progress.md`; review findings resolved or deferred here
