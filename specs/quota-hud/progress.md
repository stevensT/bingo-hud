# Quota HUD — Progress

updated: 2026-08-30
status: Phase 3 complete — checkpoint 3.9 passed
blockers: none
next_session: Start Phase 4. The three policy units at 4.1, 4.2 and 4.3 are `[P]` and run
concurrently; 4.4a is the new task covering `UsageClient` and the status-code taxonomy, and it
has to land before `QuotaMonitor` at 4.5. Confirm reality first with `dotnet build` and
`dotnet test`; both were green at 3.9 with 173 passing tests.

## Notes carried into execution

- Fixtures already exist at `tests/fixtures/usage/`: a 200 and a 401, both scrubbed and dated.
  The states still missing are listed in that directory's README and can only be captured while
  the account is actually in them.
- The status line spike is running. Its outcome decides what Phase 6 is, at gate G.1.
- Task 3.1 is a decision, not code, and it blocks 3.7.
- Task 6.1 is a spike that may force an amendment to AC-21.
- A packaging feature is queued behind this one: delivery through winget and Chocolatey, on top
  of the plain executable. It cannot start before 0.1.0 exists, because a manifest needs a
  released artifact to hash. Its only claim on this feature is BV.3, which now decides the
  publish shape with that delivery in mind instead of defaulting to self-contained.

## Checkpoints

### CP: Phase 1 Foundation — 2026-08-30
tests: 12 pass / 0 fail / 0 skip
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7
rework: none
criteria_met: none — Phase 1 is scaffolding and satisfies no acceptance criterion from
`spec.md`. The first criteria come in Phase 2.
issues:
- Publish output measured at 240 files / 134 MB, which is not the "single self-contained exe"
  the plan's Stack decision describes. `-p:PublishSingleFile=true` gives 8 files and a 120 MB
  exe; folding in WPF's native libraries needs
  `-p:IncludeNativeLibrariesForSelfExtract=true`. Trimming is unavailable — WPF does not
  support it. Measurements recorded in the README; how Bingo ships is undecided and belongs to
  BV.3.
- The plan's Stack row also cites "~30-50 MB idle", which does not match the disk figures and
  is presumed to mean memory. Unverified either way.
- The .NET SDK's first-run installed an ASP.NET Core HTTPS development certificate, outside the
  project directory. A side effect of the first `dotnet` invocation rather than a deliberate
  step. Removable with `dotnet dev-certs https --clean`.

verification notes:
- 1.1: a deliberately failing test was added and observed to report `Failed: 1, Passed: 1` with
  a non-zero exit code before being removed. A green suite is only meaningful once the same
  setup has been seen to go red.
- 1.3: the seam was tested by temporarily retargeting Core at `net9.0-windows`. The build fails
  at restore with `NU1201` before any test executes, so the boundary is enforced by the build
  system and not only by the guard tests.
- 1.6: the fixture tests were watched failing with a file-not-found naming the exact expected
  path, then wired in 1.7 and re-run green.

### CP: Phase 2 Parsing — 2026-08-30
tests: 121 pass / 0 fail / 0 skip
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 2.1 through 2.11
rework: one. The cross-path test written at 2.4 asserted the two parse paths produce identical
windows. That stopped being true at 2.8, when severity entered the model: the flat form carries
no severity field, so those windows are `Unknown` where the same windows read through `limits[]`
are `Normal`. The test was split rather than loosened — it now compares kind, percentage and
reset, and a second test asserts the severity difference explicitly, so nobody later "fixes" the
fallback by defaulting it to `Normal`.

criteria_assessed:
- AC-1 (a percentage for both windows): the parsing half is met. Both windows come out of the
  baseline capture with their utilizations and reset instants, through either path. Displaying
  them is Phase 6, so the criterion is supported but not yet met.
- AC-2 (consumed by default): met in Core, which is the whole of Core's part in it. Utilization
  is stored exactly as the server sends it and nothing in Core inverts it. The default and the
  label are settings work in Phase 6.
- AC-9 (unparseable response shows an error state and no percentages): met structurally.
  `Unreadable` carries no snapshot, so there is no percentage to display — the type makes the
  criterion unbreakable rather than merely satisfied. The error copy is 7.1.
- AC-10 (authentication failure shows a sign-in state): partially met. A 401 body naming
  `authentication_error` is read as an invalidated token, and any other body stays unspecified.
  What is missing is the step before it — deciding which status codes are authentication
  failures at all — which belongs to the client in Phase 4. See the open issue below.
- AC-12 (never a percentage from estimation, interpolation, or a hardcoded limit): met in Core.
  No plan limit appears anywhere in the codebase, nothing is interpolated, and a known window
  that is present but unreadable produces `Unreadable` rather than a zero.

issues:
- `AuthFailureKind` has a fourth value the plan's data model does not list: `Unspecified`. The
  plan calls for a 401 with an unparseable body to "stay generic", and the three planned values
  are all specific claims — `SignedOut` and `PermissionDenied` are both statements about the
  credential file that a 401 does not support. `Unspecified` is the honest reading of "the
  server said no and did not say why". Cost: one more state for the error copy at 7.1. The
  credential-side kinds are not defined yet; they arrive in Phase 3 with the tests that produce
  them.
- No task owns mapping a status code to the error taxonomy. The plan has the table — auth on
  401/403, transient on 429 and 5xx, unsupported otherwise — but Phase 2 only covers the body
  of an auth failure, and Phase 4 only names 429 explicitly at 4.8. AC-10 cannot be fully met
  until something owns that routing. It belongs with the client; the task list needs a task for
  it in Phase 4.
- Two fields from the plan's data model do not exist yet, deliberately: `QuotaSnapshot` has no
  `WorstReported` and `QuotaWindow` has no `Label` or `ModelScope`. Nothing needs them until
  `SeverityPolicy` at 4.2 and the per-model caps at 6.8, and adding them now would mean fields
  no test could justify. Flagged so their absence reads as a decision rather than an oversight.
- A third fixture exists that is not a capture: `2026-08-30-derived-flat-only.json`, the
  baseline body with its `limits` array removed. The flat fallback cannot be captured from this
  account, because the endpoint returns `limits[]` on every request. Recorded in the fixture
  directory's README, with the rule that it is regenerated by re-deriving rather than edited.

verification notes:
- Six tests in this phase pass the moment they are written, because they fence behaviour the
  parser already has. Each was proved by mutation instead, and each mutation's observed result
  is recorded in the doc comment of the tests that caught it.
- 2.3: with the parser storing `100 - percent`, six tests failed — all four direction tests plus
  the two percentage assertions from 2.1.
- 2.6: with the flat reader recognizing any top-level object holding a numeric `utilization`,
  twelve failed — all ten window-shaped unknown keys plus both derived-fixture assertions. The
  live payload's `nimbus_quill` is exactly that shape, so this is the mutation that would
  actually have shipped.
- 2.7: with the limits reader skipping any entry whose `resets_at` is not a string, exactly the
  four limits-path null-reset tests failed and the two flat-path ones did not.
- 2.11: with `limits` renamed to `quota_limits` in the fixture — the shape of a real upstream
  rename — fourteen contract assertions failed while the parser tests very largely did not. The
  parser had fallen back to the flat keys and gone on producing correct-looking windows. That
  silent success is the reason the contract test exists.
- 2.11 also caught its own author: the first run failed because `limits` was missing from the
  expected list of top-level keys.

### CP: Phase 3 Credentials — 2026-08-31
tests: 173 pass / 0 fail / 0 skip
build: pass (`dotnet clean` then `dotnet build`, 0 warnings, 0 errors)
done: 3.1 through 3.8
rework: none

decision: 3.1 settled — Bingo never refreshes the token. Recorded in `plan.md` with its
reasoning and with the single observation that would reopen it. The refresh task became a fence
test instead, and two risk rows were replaced by the two risks that actually remain.

criteria_assessed:
- AC-10 (authentication failure shows a sign-in state): still partial, and for the same reason
  as at 2.12. Phase 3 adds the front half — a missing, malformed, or tokenless credential file
  reads as no credential, and an expired token is read normally rather than as a failure. The
  missing piece is unchanged: nothing yet maps a status code to a `FetchOutcome`. That is task
  4.4a.
- AC-11 (permission denied reported differently from signed out): met in Core.
  `CredentialProbe` distinguishes readable, absent, access-denied, and busy, and the two probes
  exist because neither alone is conclusive — an unlistable directory raises the same exception
  as a missing file. The wording the user sees is 7.1.

issues:
- `AuthFailureKind.SignedOut` and `PermissionDenied` still have no producer, and `Phase 3`
  arrived at the same distinction from a different direction: `CredentialAvailability`. The two
  enums overlap and need reconciling where a credential failure becomes a `FetchOutcome`, which
  is Phase 4. The likely answer is that the availability values are the real ones and those two
  `AuthFailureKind` members are redundant — but that is a decision for whoever writes the
  mapping, not one to make in advance.
- `ICredentialProvider` does not exist yet. The plan lists it as a seam, and its stated purpose
  is to be the test double for `QuotaMonitor` — which does not exist either. Adding it now would
  be an interface with one implementation and no consumer. It arrives with 4.5.
- `CredentialAvailability` has a fourth value the plan does not mention: `Busy`, for a file held
  exclusively by another process. Claude Code rewriting the token is exactly that, and the
  advice it calls for — wait — is neither of the other two answers.
- The access-denied test produces its exception by putting a directory where the file belongs,
  which raises the same `UnauthorizedAccessException` a denying ACL raises. A real ACL denial
  cannot be set up portably from a test. The exception path is identical; the ACL itself is
  untested.
- Tests that need real files write under `bin/.../test-scratch`, inside the project tree, so a
  test run touches nothing outside it and `dotnet clean` removes any leftovers.

verification notes:
- The real credential file was never read. Its shape is known from the project's capture script,
  and every test fixture here is written to match that shape.
- 3.7: the fence was proved by adding a single `Process.Start` call to Core and watching the
  test fail naming `System.Diagnostics.Process`, then removing it.
- 3.2: the token-redaction test earns its place. A positional record generates a `ToString` that
  prints every property, so without the override the access token would have been one
  interpolated string away from any log line or debugger watch.
