# Scoped Cap Shape

## Overview

On 2026-11-11 Bingo started showing "Response unreadable" for every Claude reading. The usage
endpoint had begun reporting a per-model weekly cap, for Fable, as a third entry in its `limits`
array. That entry's `scope` came back as an object naming a model, where the parser had expected
a string. `scope` had been `null` in every earlier capture, so the string form was never more than
a prediction. The parser's rule that a scope in the wrong shape makes the response unreadable did
its job, but it also threw away the 5-hour and weekly readings, which were perfectly readable.

The same investigation turned up a second defect. The capture script took the first `accessToken`
it found anywhere in the credential file. That file now also holds tokens for connected plugins,
so the script authenticated with one of those and recorded a 401. The app was not affected: it
reads Claude's own sign-in by name.

This feature teaches the parser the scope shape the server actually sends, keeps the same
fail-closed rule for any scope shape that has not been seen, commits the new capture as a fixture,
and points the capture script at Claude's own token.

## User Stories

- As a Bingo user whose plan now reports a per-model weekly cap, I want my 5-hour and weekly
  readings back, and the per-model cap shown in the panel under the name the server gives it.
- As a Bingo user, I want a scope shape Bingo has never seen to be reported as unreadable rather
  than guessed at, so that a per-model cap is never shown or alerted on as the account-wide week.
- As the maintainer, I want the capture script to use Claude's own sign-in, so that a capture
  records the endpoint's answer and not a 401 caused by another product's token.

## Acceptance Criteria

- [ ] AC-1: A response whose `limits` array carries a scope of the form
      `{"model": {"id": …, "display_name": …}, "surface": null}` is read. The 5-hour and weekly
      windows come out as they do today, and the entry becomes a per-model weekly cap with its
      percentage, reset time, and severity.
- [ ] AC-2: The cap's label is `model.display_name` exactly as sent. When that is absent, null,
      or empty, the label is `model.id` exactly as sent. Bingo maps neither to another name.
- [ ] AC-3: A `scope` that is absent or null means the entry is restricted to nothing, as today.
- [ ] AC-4: Any other `scope` makes the whole response unreadable, with a reason naming what was
      wrong. This covers a string, a number, or an array; an object whose `model` is absent,
      null, or not an object; a `model` with neither a usable `display_name` nor a usable `id`;
      and a `surface` that is present and not null, whatever `model` holds. Unknown extra keys
      inside `scope` or `model` are ignored, as unknown keys are everywhere else.
- [ ] AC-5: The 2026-11-11 capture is committed as a scrubbed fixture with its metadata and an
      inventory row. The parser reads it as AC-1 describes, with the cap labelled "Fable", and a
      contract test pins the shape of its `limits` entries, so that a further change to the scope
      shape fails a test.
- [ ] AC-6: When the credential file has a `claudeAiOauth` object, the capture script uses the
      token inside it, never one found elsewhere in the file. It still reports which path the
      token came from, and still never prints the token.
- [ ] AC-7: Every committed description of `scope` as a string, or as a field no capture has
      carried a value in, is updated to describe the observed object, and the changelog's
      Unreleased section records the fix.

## Non-Goals

- **The other new response fields.** `seven_day_breakdown`, `weekly_scoped_shares`, `spend`,
  `extra_usage`, and the codename keys are not read.
- **Surface-scoped caps.** A scope with a `surface` is unreadable until one is captured (AC-4).
- **A string-form scope.** Dropped, not kept as a fallback. It was never observed.
- **Notifications or HUD space for per-model caps.** They stay in the detail panel only, as now.
- **Friendlier labels.** No mapping from `display_name` or `id` to another name.
- **Cutting a release.** This changes how the endpoint is parsed, so it is a MINOR change under
  the README's versioning rule. The version bump and release are decided after this closes.

## Open Questions

None. Trevor decided the four open choices on 2026-11-11: an unseen scope shape is unreadable,
the string form is dropped, the label is `display_name` then `id`, and the release is decided
afterwards.

## Dependencies

- `specs/quota-hud/spec.md` AC-23 (per-model caps in the detail panel) and the parser rules from
  `specs/audit-fixes/spec.md` AC-5, which this must not regress.
