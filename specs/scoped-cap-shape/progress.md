# Scoped Cap Shape — Progress

updated: 2026-11-11
status: Phase 2 checkpoint passed; awaiting the live app check and review
blockers: none
next_session: Trevor runs the app against the live endpoint, then the review gate

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
