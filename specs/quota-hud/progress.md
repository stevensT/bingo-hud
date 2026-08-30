# Quota HUD — Progress

updated: 2026-08-30
status: Phase 1 complete — checkpoint 1.8 passed
blockers: none
next_session: Start Phase 2 at task 2.1. Confirm reality first with `dotnet build` and
`dotnet test` — both were green at 1.8 with 12 passing tests. Phase 2 is the highest-risk work
in the project and every task in it touches `UsageNormalizer`, so none of it parallelises.

## Notes carried into execution

- Fixtures already exist at `tests/fixtures/usage/`: a 200 and a 401, both scrubbed and dated.
  The states still missing are listed in that directory's README and can only be captured while
  the account is actually in them.
- The status line spike is running. Its outcome decides what Phase 6 is, at gate G.1.
- Task 3.1 is a decision, not code, and it blocks 3.7.
- Task 6.1 is a spike that may force an amendment to AC-21.

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
