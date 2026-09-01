# Quota HUD — Tasks

## Status Legend

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress
- `[P]` Parallelizable — executed concurrently as subagents
- `[C]` Checkpoint — stop and verify before continuing

Task ids are stable. Acceptance criteria from `spec.md` are cited per task so checkpoint audits
have something concrete to check against.

## Build and test commands

Not yet verified — no solution exists. Task 1.1 creates it and task 1.2 records the real
invocations in the README's Building section. Until then the expected commands are:

```
dotnet build
dotnet test
dotnet publish -c Release -r win-x64 --self-contained
```

---

## Phase 1: Foundation

- [x] 1.1 Scaffold the solution: `BingoHud.sln`, `src/BingoHud.Core` (net9.0), `src/BingoHud.App`
      (net9.0-windows, WPF), `tests/BingoHud.Core.Tests` (xUnit). Verify by adding one test that
      passes and one that deliberately fails — confirm the runner reports the failure — then
      delete the failing one. A test runner that cannot fail is not proof of anything.
- [x] 1.2 Record the verified `build`, `test`, and `publish` invocations in the README's Building
      section, replacing the placeholder.
- [x] 1.3 Confirm `BingoHud.Core` has no reference to WPF and cannot acquire one. The seam is
      meant to be compiler-enforced, so prove the compiler enforces it.
- [x] 1.4 Test: `TestClock` returns exactly the instant it was given; `SystemClock` returns an
      instant within a second of now.
- [x] 1.5 Implement `IClock`, `SystemClock`, `TestClock`.
- [x] 1.6 Test: the test project can load `tests/fixtures/usage/2026-08-30-baseline.json` at
      runtime and it parses as JSON. Fixture files not reaching the output directory is a common
      and confusing failure — catch it here, not inside a parser test.
- [x] 1.7 Wire the fixtures into the test project so 1.6 passes.
- [x] 1.8 Checkpoint passed 2026-08-30: `dotnet build` and `dotnet test` both green (12 tests),
      task marks audited, result and three open issues recorded in `progress.md`.

## Phase 2: Parsing

Highest risk in the project. It goes first, it goes alone, and every task here touches
`UsageNormalizer` — so none of it is parallelizable.

- [x] 2.1 Test: the baseline fixture yields two windows via the `limits[]` path — session at 12
      used, weekly_all at 37 used — with `resets_at` parsed to the correct instant. Covers the
      microsecond precision and `+00:00` offset the live payload actually uses. (AC-1)
- [x] 2.2 Implement the `limits[]` primary path.
- [x] 2.3 Test: percentages are carried through as consumed, unchanged from the server, with no
      inversion anywhere in Core. (AC-2, AC-12)
- [x] 2.4 Test: with `limits` absent, the flat `five_hour` / `seven_day` keys and their alias map
      produce the same two windows. Build the fixture by deriving it from the baseline capture.
- [x] 2.5 Implement the flat fallback path.
- [x] 2.6 Test: the ten unknown top-level keys in the baseline fixture — `tangelo`,
      `nimbus_quill`, `iguana_necktie` and the rest — produce no windows and no failure. This is
      the rule that would otherwise make every real response unreadable.
- [x] 2.7 Test: a window reporting a utilization with `resets_at` null parses, keeps its
      percentage, and carries a null reset. It is neither dropped nor treated as malformed.
- [x] 2.8 Test: a severity string outside the known set maps to `Unknown`, never to `Normal`.
- [x] 2.9 Test: a response with no recognizable window at all returns `Unreadable` and no
      percentages. Never a zero. (AC-9, AC-12)
- [x] 2.10 Test: the 401 fixture maps to `AuthFailed(Invalidated)` on `error.type ==
      "authentication_error"`; a 401 with an unparseable body stays generic. (AC-10)
- [x] 2.11 Contract test pinning the known-good shape of the baseline fixture. When this fails it
      means the payload moved: recapture with `scripts/capture-usage.js`, never loosen the parser.
- [x] 2.12 Checkpoint passed 2026-08-30: `dotnet clean` then `dotnet build` and `dotnet test`
      both green (121 tests, 0 warnings), task marks audited, AC-1, AC-2, AC-9, AC-10 and AC-12
      assessed, and the result recorded in `progress.md` with four open issues and the
      mutation results for the six tests that pass on arrival.

## Phase 3: Credentials

- [x] 3.1 Decided 2026-08-31: Bingo never refreshes the token. It reads
      `~/.claude/.credentials.json` and nothing else; an expired token surfaces a sign-in state.
      Claude Code maintains the token, and an expired one means Claude Code has not run for over
      eight hours — so quota has not moved and the unrefreshable reading is an unchanged one.
      Shelling out to `claude -p .` would have spent real quota to read a quota number, which is
      the same objection that made the Messages-API fallback a non-goal. The decision, its
      reasoning, and what would reopen it are recorded in `plan.md`. Removes the shellout and its
      two risk rows; 3.7 is cut to a fence test.
- [x] 3.2 Test: `FileCredentialProvider` reads `claudeAiOauth.accessToken`, a bare root
      `accessToken`, and returns null for a missing or malformed file. Never logs the token.
- [x] 3.3 Implement `FileCredentialProvider`.
- [x] 3.4 Test: `expiresAt` in seconds and in milliseconds both normalize correctly, split on the
      `10_000_000_000` boundary. The live credential stores milliseconds — pin that case.
- [x] 3.5 Test: the watch signature over path, size and mtime changes when the file changes and
      is stable when it does not.
- [x] 3.6 Test: the two-probe check distinguishes "exists but access refused" from "absent", so
      recovery advice points the right way. (AC-11)
- [x] 3.7 Fence test, replacing the refresh path cut by 3.1: nothing in Core starts a process.
      The shellout is the kind of thing that gets reintroduced by a later contributor solving a
      symptom, so the absence is asserted rather than assumed.
- [x] 3.8 Test: token expiry is an ordinary expected transition, not an error branch. The
      observed token life is roughly eight hours, so an always-on app crosses it daily.
- [x] 3.9 Checkpoint passed 2026-08-31: `dotnet clean` then `dotnet build` and `dotnet test`
      both green (173 tests, 0 warnings), task marks audited, AC-10 and AC-11 assessed, and the
      result recorded in `progress.md` with five open issues. AC-11 is met in Core; AC-10 stays
      partial until 4.4a owns the status-code mapping.

## Phase 4: Polling and state

The three policy units below are pure functions in separate files with no shared state, so they
are genuinely independent and run concurrently.

- [x] 4.1 Test + implement `PollPolicy.NextDelay` as a data-driven table over `PollSignals`.
      Returns a delay and a named reason. Never below the two-minute floor; honours
      `ServerRetryAfter`. (AC-25)
- [x] 4.2 Test + implement `SeverityPolicy.Evaluate`: three discrete states at 25% and 10%
      remaining, worst-window drives the overall result, a `Frozen` reading is excluded from that
      calculation, and a server-reported rejection surfaces distinctly from a local threshold.
      (AC-4, AC-5, AC-6, AC-13)
- [x] 4.3 Test + implement reset formatting: absolute when distant, relative as it nears, local
      time, and nothing at all when `resets_at` is null. (AC-3)
- [x] 4.4 Checkpoint passed 2026-08-31: `dotnet clean` then `dotnet build` and `dotnet test`
      both green (417 tests, 0 warnings), no integration conflicts between the three units, and
      the result recorded in `progress.md` with three decisions and two open issues.
- [ ] 4.4a Test + implement `UsageClient` and the status-code taxonomy. Nothing yet owns the
      mapping from a status code to a `FetchOutcome`, so AC-10 cannot be met without it: 401 and
      403 are auth failures whose body goes to `UsageNormalizer.ClassifyAuthFailure`, 429 and 5xx
      are `Transient` honouring `Retry-After`, and any other status is `Unsupported`. Pins the
      request headers too — the `anthropic-beta` value and the `claude-code/<version>`
      User-Agent, which prior art reports is what keeps the endpoint out of a stricter 429
      bucket. Test against a stub message handler; never call the live endpoint from a test.
      (AC-10)
- [ ] 4.5 Test: `QuotaMonitor` holds one refresh in flight at a time — concurrent requests
      collapse to a single fetch. (AC-27)
- [ ] 4.6 Test: the full `ReadingState` transition set — first success, staleness by age, the
      `Frozen` case, a failure preserving the last snapshot with its age, and `Unreadable`
      showing no percentages. (AC-8, AC-13)
- [ ] 4.7 Implement `QuotaMonitor` with single-flight and backoff.
- [ ] 4.8 Test: a 429 backs off, honours `Retry-After`, and triggers no compensating request
      against any other endpoint. (AC-26)
- [ ] 4.9 Test: manual refresh obeys the same backoff as automatic polling, and when refused says
      why and when the next attempt is possible. (AC-28)
- [C] 4.10 Checkpoint: full suite green, AC-3, AC-4, AC-5, AC-6, AC-8, AC-10, AC-13, AC-25 through AC-28
      assessed, recorded in `progress.md`.

## Phase 5: Alerts

- [ ] 5.1 Test + implement `AlertKey` identity as window kind, threshold, and `resets_at`, so
      rearming on reset falls out of the identity rather than needing its own mechanism.
      (AC-17)
- [ ] 5.2 Test + implement `AlertEngine`: crossing a threshold raises a notification, and each
      threshold fires at most once per window occurrence. (AC-14, AC-15)
- [ ] 5.3 Test + implement `AlertStateStore`: plain JSON, survives restart mid-window, prunes
      keys for windows that have already reset. (AC-16)
- [ ] 5.4 Test + implement muting the current window's alerts. (AC-18)
- [C] 5.5 Checkpoint: full suite green, AC-14 through AC-18 assessed, recorded in `progress.md`.

## Gate: spike outcome

- [C] G.1 Read the result recorded in `specs/quota-hud/spikes/statusline-probe.md` and decide
      what Phase 6 actually is, before spending anything on it. The three outcomes in that
      document's decision table lead to: build the HUD as specified; cut it to a
      notification-only tool; or reconsider the readout premise entirely. Record the decision and
      the reasoning in `progress.md`. Phase 6 does not start until this gate is passed.

## Phase 6: Shell

Everything Windows-specific is concentrated here, deliberately, so that what it renders is
already known-correct.

- [ ] 6.1 Timeboxed spike: prove that click-through and clickability can coexist. A window with
      `WS_EX_TRANSPARENT` receives no mouse input, so it cannot observe the hover that would
      toggle hit-testing back on. Establish whether a cursor-position timer or a low-level mouse
      hook is required, and write down which. If neither works cleanly, AC-21 needs amending
      before any HUD code is written. (AC-21)
- [ ] 6.2 Test + implement settings persistence in Core: position, collapse preference, display
      direction, and thresholds. Testable headless, so it is not deferred into the UI. (AC-22)
- [ ] 6.3 Implement the HUD window: frameless, always-on-top, draggable. (AC-19)
- [ ] 6.4 Implement edge snapping. (AC-20)
- [ ] 6.5 Implement click-through per the 6.1 outcome, with explicit coverage of the enter and
      leave transitions. (AC-21)
- [ ] 6.6 Implement the readout: both windows, percentage labelled with its direction, reset
      alongside on the same line. (AC-1, AC-2, AC-2a, AC-2b, AC-3)
- [ ] 6.7 Implement collapse behaviour: default shows both windows; when enabled, show only the
      worst unless both are non-normal. (AC-7)
- [ ] 6.8 Implement the detail panel: per-model weekly caps, exact reset times, current status,
      app version, and last successful poll time. Per-model caps have been null on every capture
      so far, so the empty state is the common case and must be designed, not an afterthought.
      (AC-23, AC-24)
- [ ] 6.9 Implement the tray icon and menu.
- [ ] 6.10 Wire toasts to `AlertEngine`. (AC-14)
- [ ] 6.11 Confirm all P/Invoke lives in one file and nothing else in `App` calls into Win32.
- [C] 6.12 Checkpoint: full suite green, build green, AC-1 through AC-3, AC-7, AC-19 through
      AC-24 assessed, recorded in `progress.md`.

## Phase 7: Polish

- [ ] 7.1 Write the error copy for every state: sign-in, permission-denied, unreadable,
      unavailable, unsupported, stale, frozen. Each says what happened and what to do about it.
      (AC-9, AC-10, AC-11)
- [ ] 7.2 Close the spike: record the result in the spike document, delete
      `scripts/quota-statusline.ps1`, and remove the `statusLine` entry from settings.
- [ ] 7.3 Set `<Version>` to `0.1.0` in `BingoHud.App.csproj`, move the `[Unreleased]` entries
      into a dated `0.1.0` section in `CHANGELOG.md`, and confirm the version the app displays
      matches. The git tag is Trevor's to apply.
- [C] 7.4 Checkpoint: full suite green, build green, every acceptance criterion assessed against
      the spec, recorded in `progress.md`.

## Build Verification

- [ ] BV.1 `dotnet build` exits clean.
- [ ] BV.2 `dotnet test` — full suite, no failures, no skips left unexplained.
- [ ] BV.3 Produce a runnable exe, and choose the publish shape deliberately rather than by
      default. Self-contained was assumed on the grounds that a user should not have to install
      a runtime — but that argument only holds for a hand-delivered download. Bingo is also
      intended to be installable through a package manager, and both winget and Chocolatey can
      declare the .NET 9 Desktop Runtime as a dependency and install it first, which reduces the
      artifact from the measured ~120 MB to a few MB. Record the choice, the reasoning, and the
      measured figures in the README's Building section, replacing the open "how Bingo ships"
      note. Packaging itself is not this feature's work.
- [ ] BV.4 Launch the published exe on a clean path and confirm it reads quota and renders.
- [C] BV.5 Final checkpoint: all builds green, all tests pass, acceptance criteria verified
      against `spec.md`, result recorded in `progress.md`.
