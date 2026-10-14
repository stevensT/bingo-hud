# Spike: Codex session log probe

**Status:** open — written and criteria agreed 2026-10-13; first run 2026-10-13; due to close no earlier than 2026-10-21
**Script:** `scripts/codex-ratelimit-probe.js`

## Question

Can the rate-limit record that Codex writes into its own session logs serve as Bingo's Codex
source — consistently shaped, in agreement with what Codex itself shows, and fresh enough to put
on screen — without Bingo calling any endpoint?

## Why this spike exists

There are two ways to learn the Codex quota. One reads the session logs Codex already writes to
`%USERPROFILE%\.codex\sessions\` and `archived_sessions\`, where each turn records a
`rate_limits` object. The other calls the endpoint Codex calls, with the token in
`%USERPROFILE%\.codex\auth.json`. Everything in the feature after the source — the model, the
display, the alerts — is shaped by which one is used, so it is the decision to settle first.

The log route is the attractive one: no network, no token, nothing to rate-limit, and the README's
"nothing leaves your machine" stays true. It is also unproven on every point that matters. The log
format is undocumented; a `rollout-migrations` folder beside it says it has changed before; and on
the day this was written, the logs already carried two Codex versions (`0.147.0-alpha.6.6` and
`0.162.0-alpha.17.2`) from two originators (`Codex Desktop` and `codex_work_desktop`). If the
record moves or drifts from the server, Bingo would show a Codex figure as current when it is not,
which is the failure constitution principle 6 exists to prevent.

The cost of finding out late is high: a Codex reader, its fixtures, and the display work built on
it would all be rebuilt around a different source.

## What was observed before the experiment

One record, from the newest log on 2026-10-13:

```
"rate_limits":{"limit_id":"codex","limit_name":null,
  "primary":{"used_percent":21.0,"window_minutes":300,"resets_at":1791969804},
  "secondary":{"used_percent":11.0,"window_minutes":10080,"resets_at":1792536462},
  "credits":{"has_credits":false,"unlimited":false,"balance":"0"}}
```

`primary` is a five-hour window and `secondary` a weekly one, each with a consumed percentage and
a reset time in Unix seconds. The weekly reset falls on Tue 2026-10-20 15:47 local time.

Only seven logs exist: three from 2026-08-12 and four from 2026-10-13. That is too little
history to answer the question, so the probe has to run forward.

## Method

1. **A read-only script**, `scripts/codex-ratelimit-probe.js`, run by hand. It walks both log
   folders and, for every line that carries a `rate_limits` field, keeps that object, the line's
   timestamp, the file name, and the log's `cli_version` and `originator`. It prints a summary:
   - every distinct shape of the `rate_limits` object (its key set, nested), with a count
   - every `limit_id` and `window_minutes` value seen
   - any record missing a field, or with a null percentage or reset
   - per log, the number of records and the longest gap between them while the session was active
   - the latest reading, with its age
   - for each window, whether a record taken after its `resets_at` shows the percentage falling
2. **Comparison against Codex's own figure.** On at least five occasions across the week, note
   what Codex shows for usage beside what the script reports as the latest reading, with the time
   and whether Codex was used on this machine in between. Record each pair under Findings.
3. **Duration.** At least seven days of ordinary Codex use, spanning several five-hour resets and
   the weekly reset on 2026-10-20. Run the script at least once a day, and again after any Codex
   update.

### Privacy

The logs hold the user's conversations. The script parses a line only once it has matched
`rate_limits` or the session header, keeps only the fields listed above, and prints nothing else.
It writes no files. Nothing it prints is committed; findings are summarised into this document by
hand.

## What this does not test

- **The endpoint route.** Whether the endpoint Codex calls is usable, what it returns, and what
  calling it with Codex's token would risk. If this spike rules the logs out, that is a second
  spike.
- **Usage from elsewhere.** Codex used on another machine or in the browser consumes the same
  quota without writing a log here. The comparison in step 2 can show that the logs lag; it
  cannot measure how often that happens for other people.
- **Accounts other than this one**, API-key sign-in, or plans other than the one in use here.
- **Display, alerts, settings**: everything after the source.
- **Whether the readout is wanted.** The original value spike for the HUD answered this for
  Claude, and the evidence is taken to carry over. If the week shows Codex is barely used, that
  is recorded, and it weighs against the feature regardless of the source.

## Deliberate deviations from the constitution

**Principle 1, test-first, is suspended for this spike.** The script is throwaway and its only
output is a decision. The reader that may follow gets tests first, built on fixtures scrubbed
from what this spike observes.

**Principle 6 stays in force.** The script prints the age of every reading it reports as latest,
and never prints a percentage for a window whose reset has passed without a newer record.

## Decision criteria

| Observed | Reading | Consequence |
|---|---|---|
| One shape across every record and version; every comparison agrees with Codex to the point and to the reset; records appear in every active session | The logs are a sound source | Plan on the log route. Shapes seen become the first fixtures. |
| Shape consistent, but a comparison shows Codex ahead of the log with no local use in between | The logs miss usage from elsewhere | Logs alone are not enough. Open the endpoint spike: under spec AC-3b the logs stay the local source, unmarked, and the endpoint supplies account-wide readings, marked with the remote icon. |
| Shape changes across versions or originators, or records are missing from some active sessions | The logs are too fragile to read | Do not plan on the logs. Open the endpoint spike. |
| A window's percentage does not fall after its reset in any record taken after it | The reset field cannot be trusted | Same as the row above. |
| Codex used on fewer than three days of the seven | The feature may not earn its place | Stop and reconsider the feature before choosing a source. |

A `limit_id` other than `codex`, or a third window, does not decide anything here. Record it; it is
evidence for whether Codex has per-model caps the way Claude does.

## Exit

When the spike closes, record the result below and delete `scripts/codex-ratelimit-probe.js`. It is
committed so the experiment is reproducible, not because it is a deliverable.

## Findings

**2026-10-13, first run: 7 logs, 131 records.**

- **The older version wrote no figures.** All three logs from `Codex Desktop 0.147.0-alpha.6.6`
  (2026-08-12) have one record each, and each is `rate_limits: null`. The 128 records from
  `0.162.0-alpha.17.2`, written by both originators, share one shape. So the shape has already
  changed once across versions. That is a change from nothing to something rather than a field
  moving, but it means any reader has to treat a null record as "no reading", never as 0%. It
  does not trigger the "shape changes" row yet; whether it does depends on what the next version
  writes.
- **One reset observed, and it behaved.** The five-hour window reset at 8:47 PM. The last record
  before it read 50%, and the first after it, at 9:23 PM, read 0% with a later reset time.
- **Records arrive every turn but not continuously.** The longest gap inside one session was
  99 minutes, presumably idle time. One log has no records at all.
- **Values:** `limit_id` was always `codex`, the plan was `plus`, and the windows were always 300
  and 10080 minutes.
- **Comparisons with Codex's own figure:** none yet.

## Result

Open.
