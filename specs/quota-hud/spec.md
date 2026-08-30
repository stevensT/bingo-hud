# Quota HUD

## Overview

Claude Code on a Max/Pro subscription is governed by rolling usage windows, not by a per-token
dollar cost. The constraint that actually bites is running out of window mid-task — and today the
only way to check is to stop working and run `/usage` in a session. That is exactly the wrong
moment to find out, and the interruption is the cost you were trying to avoid.

Quota HUD puts the answer in peripheral vision: a small always-on-top readout showing how much of
the 5-hour and weekly windows remain, and when each resets. It answers one question continuously —
*can I keep working?* — so the answer arrives before it becomes urgent rather than after.

## User Stories

- As a Claude Code user on a subscription plan, I want to see remaining quota without leaving what
  I'm doing, so that I don't have to interrupt a session to find out where I stand.
- As a user deep in a task, I want to be warned as I approach a limit, so that I can choose a
  stopping point instead of being cut off at an arbitrary one.
- As a user planning my day, I want to see when each window resets, so that I can decide whether
  to push on now or wait.
- As a user who has been rate-limited, I want that state shown unambiguously, so that I stop
  attributing failures to something else.
- As a user relying on a glanceable number, I want to know when that number is stale or
  unavailable, so that I never act on a figure that has quietly stopped being true.
- As a user reporting a problem, I want to see which version I'm running, so that a fault can be
  tied to a specific build when the upstream payload changes.

## Acceptance Criteria

### Readout
- [ ] AC-1: The HUD displays a utilization percentage for both the 5-hour and weekly windows.
      AC-2 governs which direction that percentage is expressed in.
- [ ] AC-2: Percentages are displayed as **consumed** by default, matching what `/usage` reports,
      so the two never need reconciling in the reader's head.
- [ ] AC-2a: Display direction is a user setting — consumed or remaining.
- [ ] AC-2b: The figure on screen states which direction it is in, in both settings. A percentage
      whose meaning depends on a setting the reader cannot see is a number that can be read
      exactly backwards.
- [ ] AC-3: Each window's reset time appears alongside its percentage, on the same line — absolute
      when distant ("resets 4:38 AM"), switching to relative as it nears ("resets in 53 min"), in
      local time.
- [ ] AC-4: Severity is shown in three discrete states, not a continuous gradient: normal, warning
      at 25% remaining, critical at 10% remaining.
- [ ] AC-5: Overall severity is driven by the worst window of the two.
- [ ] AC-6: A server-reported rate-limited state is surfaced distinctly from a locally-derived
      threshold warning.
- [ ] AC-7: Collapsing is a user setting. Default shows both windows at all times; when collapse is
      enabled, the HUD shows only the worst window unless both are in a non-normal state.

### Honesty
- [ ] AC-8: Every reading carries an age, and the HUD shows that age once a reading is stale.
- [ ] AC-9: When the response cannot be parsed, the HUD displays an explicit error state and no
      percentages.
- [ ] AC-10: When authentication fails, the HUD displays an explicit sign-in state and no
      percentages.
- [ ] AC-11: A "permission denied" credential failure is reported differently from a "signed out"
      failure, so recovery advice points the right way.
- [ ] AC-12: The HUD never displays a percentage derived from estimation, interpolation, or a
      hardcoded plan limit.
- [ ] AC-13: A reading that can no longer be refreshed is frozen and marked, and does not
      determine overall severity.

### Alerts
- [ ] AC-14: Crossing a threshold raises a desktop notification.
- [ ] AC-15: Each threshold notifies at most once per window occurrence.
- [ ] AC-16: Alert state persists across app restarts within the same window occurrence.
- [ ] AC-17: Alert state rearms when a window resets.
- [ ] AC-18: The current window's alerts can be muted.

### Window behaviour
- [ ] AC-19: The HUD is frameless, always-on-top, and draggable.
- [ ] AC-20: The HUD snaps to screen edges.
- [ ] AC-21: The HUD is click-through when idle, so it does not intercept clicks meant for what's
      beneath it.
- [ ] AC-22: Position, collapse preference, display direction, and threshold settings persist
      across restarts.
- [ ] AC-23: Clicking the HUD opens a detail panel showing per-model weekly caps, exact reset
      times, and current status.
- [ ] AC-24: The detail panel shows the running application version and the time of the last
      successful poll.

### Behaviour toward the upstream service
- [ ] AC-25: Poll interval is adaptive within a 2–30 minute range, never faster.
- [ ] AC-26: A rate-limited response backs off and never triggers a compensating request against
      any other endpoint.
- [ ] AC-27: Only one refresh is in flight at a time.
- [ ] AC-28: A manual refresh is available from the detail panel. It is subject to the same backoff
      as automatic polling and cannot be used to poll faster than the floor allows; when refused,
      it says why and when the next attempt is possible.

## Non-Goals

- **Per-project or per-session token attribution.** Answering "what did I spend, and where"
  requires transcript ingestion — a file walker, a multi-tier deduplication scheme, and a store.
  It is a separate project and answers a different question than "can I keep working."
- **Token counts of any kind.** The quota endpoint reports utilization, not tokens.
- **The Messages-API rate-limit-header fallback.** Reading quota via response headers costs a real
  request against the user's own quota.
- **Dollar cost or spend as a headline.** Irrelevant on a subscription; paid overflow is not the
  signal a subscription user acts on.
- **Burn-rate projection or "time remaining at current pace."**
- **Providers other than Anthropic, and coding agents other than Claude Code.**
- **Multiple accounts.**
- **Platforms other than Windows.**
- **Auto-update.** Versions are released and installed deliberately; the app reports its version
  but does not fetch or apply new ones.

## Open Questions

- [DEFERRED: Should the three severity states be renamed to aviation brevity codes matching the
  product name — *joker* at 25% remaining, *bingo* at 10%, *winchester* at 0%? This is not a
  rename of AC-4 but an expansion of it: *winchester* introduces an exhausted state the spec does
  not currently carry, taking the ladder from three states to four. Deferred until the HUD is
  built and the present three states have been used in anger. Does not block implementation; AC-4
  stands as written.]

## Dependencies

- **`GET https://api.anthropic.com/api/oauth/usage`** — undocumented, reverse-engineered from
  shipping clients. The sole source of quota truth. Its payload shape is known to have drifted more
  than once; it can change or disappear without notice, and AC-9 defines the required behaviour
  when it does.
- **`~/.claude/.credentials.json`** — the OAuth token Claude Code maintains. No third-party OAuth
  client registration exists for consumer subscription plans, so there is no independent
  authentication path.
- **The `claude` CLI** — token refresh is delegated to it; no OAuth refresh grant is implemented
  locally.
- **An active Claude Max or Pro subscription.**
