# Spike: the error states, on screen

**Status:** open — started 2026-09-22
**Script:** `scripts/stub-usage-server.js`

## Question

Does the built shell draw every reading state the way the tests say Core composes it? AC-9,
AC-10 and AC-11, and the stale and frozen marks, have been assessed against tests only. The tests
prove the words; they do not prove that a window shows them, fits them, or repaints when the
state changes.

## Why this spike exists

Every one of these states needs a failure to see, and there was no safe way to cause one.
Renaming the real credential file would also sign out the Claude Code session doing the
verifying, and nothing in the app can be pointed anywhere else: the credential path comes from
`GetFolderPath(UserProfile)`, which ignores environment variables, and the endpoint is a
constant.

## Method

Two overrides, compiled into Debug builds only:

- `BINGO_CREDENTIALS_PATH` — the credential file to read instead of the real one.
- `BINGO_USAGE_ENDPOINT` — the URL to fetch instead of the real endpoint.

These are permanent code, not spike code: they are tested, and a fence test holds them out of
Release. What is throwaway is `scripts/stub-usage-server.js`, a local server that answers every
request from whatever mode file it is told to read, so the state can be switched while the app
runs. The success body is the baseline fixture with its reset times moved into the future.

With the overrides set, the real token is never read and nothing reaches the real endpoint, not
even with a fake token.

| State | How it is forced |
|---|---|
| Signed out (AC-10) | Credential path to a file that does not exist |
| Permission denied (AC-11) | Credential path to a directory, which raises the same exception as a denying ACL |
| Invalidated token (AC-10) | Stub answers 401 with the recorded `authentication_error` body |
| Unreadable (AC-9) | Stub answers 200 with a body naming no window |
| Unavailable | Stub not running |
| Unsupported | Stub answers 404 |
| Frozen (AC-13) | A success, then a 401 at the next poll |
| Stale (AC-8) | A success, then the stub stops, and 45 minutes pass |
| Scoped-only regression (7.1 review) | Stub answers 200 with only a per-model window |
| Warning, critical (AC-4, AC-5) | Stub answers 200 with the session window at 80% and 95%, the weekly at 37% |
| Rate-limited (AC-6) | Stub answers 200 with the session window's severity `rejected` |

The last three rows were added while the spike ran, on finding that the severity states had not
been seen on screen either and the same stub reaches them for free.

Each is photographed from a Debug build launched fresh, except frozen and stale, which need a
reading first. The live trial instance is stopped while this runs, because both would share one
settings file and one alert store, and restarted afterwards.

## What this does not test

- A real denying ACL. The directory trick raises the same exception on the same path, as the
  credential tests already note.
- The real endpoint producing any of these. The stub answers with recorded bodies where they
  exist, and with invented ones where no capture exists.
- Release builds. The overrides are absent there by design, so Release is only checked for
  ignoring them.

## Deliberate deviations from the constitution

**Principle 1, test-first, is suspended for the stub server only.** It is throwaway, and its
only output is a set of photographs. The two overrides are production code and get tests first.

## Decision criteria

| Observed | Reading | Consequence |
|---|---|---|
| Every state draws its words legibly, and the HUD repaints on each change | AC-9, AC-10, AC-11, AC-8 and AC-13 met on screen | Record as met at 7.4 |
| A state draws blank, truncated, or unchanged after a transition | A shell defect the tests cannot see | Fix before 7.4 passes; the photograph is the reproduction |
| A state cannot be reached at all through the overrides | The method is incomplete | Record which state, and assess it against tests only, saying so |

## Exit

Record the result below, delete `scripts/stub-usage-server.js`, and keep the overrides.

## Result

**Partial, 2026-09-22. Stays open** until severity is drawn and can be photographed with the same
stub.

Every error state was reached and seen: signed out, permission denied, invalidated token,
unreadable, unavailable, unsupported, and the scoped-only case, each on the HUD and in the panel;
frozen by a success followed by a 401 at the next poll; stale after 48 minutes of 500s. The stale
reading was read through UI Automation rather than photographed, because the display had gone to
sleep by then and a sleeping display captures as black. AC-11 holds on screen: "Credential
unreadable" and "Signed out" are distinct in both headline and advice.

Found on screen, not by the tests, and fixed with a test first:

- The unreadable advice printed "recognizes.. This endpoint". The parser's reasons are whole
  sentences and were spliced in after a colon. The existing test used an invented lowercase
  fragment as the reason, which is exactly why it could never show this.
- With no reading, the panel told the user the account had no per-model caps, and left the
  windows heading bare. Both sections now say "Nothing read yet."
- After a 404, the status said polling had stopped while the next-poll row still gave a reason
  for the next poll. The row now says polling has stopped, from the same rule that stops it.

**The finding that blocks 7.4:** warning, critical and rate-limited draw exactly like normal.
Core has decided severity since Phase 4, but no task ever drew it, so AC-4, AC-5 and AC-6 are met
in Core and absent from the product. The alert for a crossing did appear, which is AC-14 and not
these.

Release was checked with both override variables set: it read the real account and ignored the
stub, as the fence says it must.
