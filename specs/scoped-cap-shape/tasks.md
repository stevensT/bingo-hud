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

## Phase 3: Review findings

Raised at review on 2026-11-11. These run in sequence.

- [x] 3.1 Test: a valid scope on a `session` and on a `weekly_all` entry gives a `WeeklyScoped`
      window labelled `Fable`. A string scope on an unknown kind is unreadable. A bad scope beside
      a valid session entry, a valid weekly entry and the flat `five_hour`/`seven_day` keys is
      unreadable. `" Fable "` is kept untrimmed. These pin behaviour already present; each is
      checked against a deliberate mutation of the parser to prove it can fail. (AC-2, AC-4, AC-9)
- [x] 3.2 Test + implement: an entry with kind `weekly_scoped` and a null or absent scope is
      unreadable with the reason `A per-model weekly entry named no model.` (AC-8)
- [x] 3.3 Test + implement: a `display_name` or `id` present as a number, boolean, object or
      array is unreadable, even beside a valid name in the other field. Split the "named no
      model" reason so a `model` that is not an object, a name of the wrong type, and a model
      with no usable name each say so. (AC-2, AC-4)
- [x] 3.4 Test: the 2026-11-11 fixture, parsed and composed, gives a panel per-model row labelled
      `Fable` with no empty state, and panel windows `5h` and `Week`. Add `ScopedCap` to
      `FixtureAvailabilityTests`. The "no alert for the cap" check was dropped: the captured cap
      is at 0%, so it would pass whatever the engine did, and `AlertEngineTests` already pins
      that per-model caps never alert. (AC-1, AC-5)
- [x] 3.5 Fix `scripts/capture-usage.js` so that when the `claudeAiOauth` key exists, the token is
      looked for only there, and a value that is null or not an object stops with its own
      message. Bring the header comment up to date. Test-first suspended as in 2.1; verify live,
      and verify the null case against a scratch credential file. (AC-6)
- [x] 3.6 Update the remaining stale descriptions: the "so far" and "common case" wording in
      `PanelReadout`, `PanelContent`, `PanelReadoutTests` and `BaselineFixtureContractTests`; the
      `UnnamedScope` summary; `specs/quota-hud/plan.md` lines on `ModelScope` and the per-model
      row; the `UsageNormalizer` class summary and `ReadScope` summary; `QuotaWindow` and
      `WindowKind` wording; the two test comments the review named. Sweep for `so far`,
      `ever been observed`, and `common case`. (AC-7)
- [x] 3.7 Checkpoint: clean, build, full suite; audit marks; all acceptance criteria; record in
      `progress.md`.

### Deferred at review

- Duplicate unscoped windows in `limits` (two `weekly_all` entries): the HUD takes the first.
  Predates this feature; listed under the spec's non-goals. Lifted by its own change adding a
  criterion that a repeated unscoped kind is unreadable.

## Build Verification

- [x] `dotnet clean`, then `dotnet build` with zero warnings
- [x] `dotnet test`, all passing
- [ ] Run the app against the live endpoint and confirm the HUD shows the 5-hour and weekly
      readings and the panel lists the Fable cap
- [ ] Final checkpoint recorded in `progress.md`; review findings resolved or deferred here
