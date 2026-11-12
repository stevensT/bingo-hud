# Scoped Cap Shape — Progress

updated: 2026-11-11
status: complete; release 0.6.0 in progress
blockers: none
next_session: release 0.6.0 from step 16 (winget-pkgs pull request) of the README's Releasing steps

Live check, 2026-11-11: Trevor ran the Debug build against the live endpoint and confirmed the HUD and the Fable row in the panel.

## How it was found

Bingo showed "Response unreadable" with the reason "An entry of the limits array had a scope that
was not a string." A capture showed a third `limits` entry, `weekly_scoped`, with `scope` set to
`{"model":{"id":null,"display_name":"Fable"},"surface":null}`. The first capture attempt returned
a 401 because the capture script picked up a plugin's token from the credential file instead of
Claude's own; a scratch copy of the script reading `claudeAiOauth` first produced the 200 that
became the fixture.

Baseline before any change: 1705 pass.

## Checkpoints

### CP: Phase 1 Parsing — 2026-11-11
tests: 1730 pass / 0 fail / 0 skip
build: pass, 0 warnings (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 1.1, 1.2, 1.3
criteria_met: AC-1, AC-2, AC-3, AC-4, AC-5
notes:
- Run inline and in sequence; all three tasks touch `UsageNormalizer` or its tests.
- 1.1 failed 9 of 13 before the fix; the 4 that passed were the null, absent, unknown-kind, and baseline cases, which already behaved.
- 1.1 gave every unaccepted shape one temporary generic reason, so that 1.2's reason assertions could fail first. 1.2 failed all 14 new cases on reason text before the specific reasons were written.
- 1.3's parser test on the fixture passed as soon as it compiled, because 1.1 already handled the shape. Its failing state was the missing fixture constant. It is a pin, not a driver.
- The class summary of `UsageNormalizerScopedCapTests` was rewritten in 1.1 rather than 2.2, since the file was being rewritten anyway.
issues: none

### CP: Phase 2 Script and docs — 2026-11-11
tests: 1730 pass / 0 fail / 0 skip
build: pass, 0 warnings (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 2.1, 2.2
criteria_met: AC-1 to AC-7
notes:
- 2.1 test-first suspended as planned. Verified live: the script reported `claudeAiOauth.accessToken` and HTTP 200, 2512 bytes. A credential file with `claudeAiOauth` but no token inside it now fails with its own message rather than falling through to another product's token.
- 2.2 sweep: no `scope string` or `never yet` wording left in `src` or the test project. README needed no change; it mentions per-model caps but not their shape.
issues: none

## Review — 2026-11-11

Four reviews of the committed work: general code, test coverage, comment accuracy, silent failures. No critical findings and no parser bug. Decisions taken by Trevor the same day:

| Finding | Outcome |
|---|---|
| Capture script fell back to other sign-ins when `claudeAiOauth` was present but null or not an object | Fixed in 3.5 |
| No test that a valid scope outranks a `session` or `weekly_all` kind | Fixed in 3.1 |
| Stale "so far" / "common case" wording the 2.2 sweep missed | Fixed in 3.6 |
| `weekly_scoped` entry with no scope skipped silently | Fixed in 3.2: unreadable (AC-8) |
| Wrong-typed `display_name` or `id` fell back silently | Fixed in 3.3: unreadable (AC-2, AC-4) |
| One reason for several model failures | Fixed in 3.3: split into three |
| Duplicate unscoped windows in `limits` | Deferred; recorded in tasks.md and the spec's non-goals |
| Unknown keys inside `scope` (e.g. `tier`) accepted | Dismissed: AC-4 ignores unknown keys by decision |
| Public-repo wording in this feature's spec files | Dismissed: matches earlier committed specs, names no tooling |

### CP: Phase 3 Review findings — 2026-11-11
tests: 1747 pass / 0 fail / 0 skip
build: pass, 0 warnings (`dotnet clean`, `dotnet build`, `dotnet test --no-build`)
done: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6
criteria_met: AC-1 to AC-9
notes:
- 3.1 pins behaviour already present, so each test was proven against a deliberate mutation of `UsageNormalizer.cs`, restored with `git checkout` after each: kind-before-scope failed both `AScopeOutranksAKindTheHudShows` rows; trimming the label failed the untrimmed test; skipping a broken-scope entry failed both whole-response tests.
- 3.2 failed both rows before the fix. 3.3 failed exactly the 9 new rows (3 model-not-object, 6 wrong-typed name) before the fix.
- 3.4's end-to-end panel test was run against the pre-fix parser from `fd3bc6d` and failed with `Unreadable`, the 2026-11-11 symptom. Its "no alert" check was dropped as vacuous on a 0% cap.
- 3.5 verified against five scratch credential files, each with a decoy plugin token: `claudeAiOauth` as null, a string, an array, `{}`, and an empty token all stop with exit 2 and never use the decoy. Live run used `claudeAiOauth.accessToken` and got HTTP 200.
- The first clean build at this checkpoint failed to copy into the App output because the running Debug app held `BingoHud.Core.dll`. The app was stopped, the clean build rerun with 0 warnings, and the app relaunched on the new build.
issues: none

## 2026-11-11: release 0.6.0

Decided after the feature closed, as the spec's non-goal said it would be. Released on its own rather than held for the Codex endpoint spike, because every installed 0.5.0 is unreadable on an account reporting a per-model cap. Following the README's Releasing steps.
- Step 1: 0.6.0, a minor version, because the change is to how the endpoint response is parsed.
- Step 2: `<Version>` set to 0.6.0; the Unreleased entry moved into a dated `[0.6.0] - 2026-11-11` section with a one-paragraph summary. Nothing else user-visible landed since 0.5.0.
- Step 3: `dotnet clean`, `dotnet build` 0 warnings, `dotnet test` 1747 pass.
- Step 4: release commit 521fc18, tagged v0.6.0 and pushed by Trevor.
- Step 5: both exes read 0.6.0+521fc182a2ee673b353dda124e042e18d1892cdb.
- Steps 6 to 8: setup compiled; the three files gathered in artifacts\release-0.6.0; Defender scan found no threats, exit 0.
- Step 9: installed 0.5.0 (starting at sign-in) started as process 62160; silent setup exited 0; Bingo restarted as process 53292; all ten checks passed with -StartsAtSignIn at 0.6.0.
- Step 10: setup 340AEF55…9EF86, self-contained 0DAB504A…30A2, framework-dependent 71AD995B…D08A. Release notes in artifacts/release-0.6.0-notes.md.
- Step 11: GitHub release "Bingo 0.6.0" published by Trevor from v0.6.0 as a pre-release, with the three files.
- Step 12: the setup downloaded from the release matched the local hash.
- Step 13: manifest written at packaging/winget/stevensT.Bingo/0.6.0, copied from 0.5.0 with the version, release date, installer URL, hash and release notes URL changed. After the install test, "resets" in ShortDescription and Description became "starts over": winget-pkgs' brand check matched "ESET" inside the word on both earlier PRs and each needed a moderator's waiver. Revalidated; the change touches no installer field, so the install test stands.
- Step 14: `winget validate` succeeded. With `LocalManifestFiles` turned on by Trevor, `winget install --manifest` downloaded the release's setup, verified its hash and reported "Successfully installed". Bingo (process 53292) was closed and restarted as process 52564; all ten checks passed with -StartsAtSignIn at 0.6.0.
