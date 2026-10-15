# Spike: Codex session log probe

**Status:** closed 2026-10-14, early, at Trevor's call — the session logs are the Codex source
**Script:** `scripts/codex-ratelimit-probe.js`, deleted on closing; it is in the history at the
commit that added it

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

**2026-10-14, 10:50 AM run: 27 logs, 158 records.**

- **The current version also writes null records, in a burst.** Nineteen new logs from `Codex
  Desktop 0.162.0-alpha.17.2` were all created at 10:03:06–07 AM, within about one second. Each
  holds exactly one rate-limit record, and it is `rate_limits: null`. They are not small: from 7
  to 1,040 lines and up to 880 KB each, read by size and line count only. Sessions with that much
  in them would normally carry a record per turn, and nineteen do not start in the same second, so
  they look like sessions written in one go (imported, restored or migrated) rather than live
  work. Not confirmed: the probe does not read the lines that would say. Until it is, this sits
  between "a null means no reading", which any reader must handle anyway, and "records missing
  from some active sessions", which is a decision row. The earlier finding that only the old
  version wrote nulls no longer holds.
- **Everything with figures still has one shape.** 136 records, both originators, version
  0.162.0-alpha.17.2: `limit_id` always `codex`, plan `plus`, windows 300 and 10080 minutes.
- **A second five-hour reset behaved.** 24% at 11:32 PM, a reset due at 2:23 AM, then 0% at
  10:21 AM with a later reset.
- **`resets_at` is not fixed for a window.** Records for that same five-hour window gave its reset
  as 2:23:16 and 2:23:24 AM, eight seconds apart. A reader cannot use the exact `resets_at` to
  tell one window from the next, which matters for anything keyed on it, such as alerts already
  shown.
- **A normal session today:** the 10:19 AM log has 8 records over a minute and a half.
- **Latest reading:** 10:22 AM, five-hour 2% used (resets 3:21 PM), weekly 12% used (resets
  2026-10-20 3:47 PM).
- **Days with Codex use so far:** 2026-10-13 and 2026-10-14.
- **The 10:03 burst explained, in part:** Trevor opened the Codex app at 10:03. So the nineteen
  logs were written by the app starting, not by nineteen sessions, and the null records are a
  start-up artefact the reader skips. Which sessions they are is still unread.
- **Comparison 1, 2026-10-14 about 10:55 AM.** Codex app: five-hour 93%, resetting 3:21 PM;
  weekly 87%, resetting Oct 20. Probe at 10:55:35, latest record 10:55:30: five-hour 8% used,
  resetting 3:21:04 PM; weekly 13% used, resetting 2026-10-20 3:47:42 PM. The app labels its
  figures as usage remaining (Trevor confirmed), so they are 7% and 13% used: the weekly agrees
  exactly, the five-hour differs by one point, and both resets agree. The log is a point ahead,
  not behind, so this is not usage the logs missed. Whether the point is rounding or a turn
  between the two readings is unknown, because the reading time was not noted. No Codex use
  elsewhere reported. Later comparisons read the app first and run the probe with no Codex use
  in between, so a gap cannot come from timing.

**2026-10-14, 2:28 PM run: 31 logs, 294 records.**

- Four new sessions since 10:55 AM, 11:14 AM to 1:02 PM, and more turns in the 10:19 one: 136 new
  records, every one the same shape as before. No new null records; the 22 are the three from
  August and the nineteen from the app starting at 10:03.
- Records kept arriving through every session; the longest gap inside one was 49.8 minutes.
- Latest reading: 1:02 PM, 1.4 hours old at the run. Five-hour 38% used, resetting 3:21 PM;
  weekly 18% used, resetting 2026-10-20 3:47 PM.
- **Comparison 2, 2026-10-14 after 2:28 PM.** Trevor's last Codex use was at 1:02 PM, the time of
  the latest record, and none since. The app showed five-hour 61% remaining, so 39% used; the
  log's latest record says 38 (the probe prints the raw value, a whole number). The app is one
  point ahead of the log with no local use in between. Weekly not read.
- **What that means against the criteria.** Read literally, "Codex ahead of the log with no local
  use in between" is the second decision row, which points to usage the logs miss. One point is
  also what a record would show if it carries the figure from before its own model call, so that
  every log trails by the cost of the last call. The table did not foresee a fixed lag of that
  kind. Trevor used Codex nowhere else, which favours the lag, but this run alone cannot rule
  out usage the logs do not see. Not decided here. Next comparisons: right after a turn, read the
  app and run the probe with nothing in between, several times. If the log trails the app by
  about one call every time, it is a lag. A positive control settles the second row directly: use
  Codex somewhere else (the browser, say), then compare without any use on this machine; a gap
  that grows by that use is usage the logs miss.

**Comparison 3, 2026-10-14 about 3:16 PM.** Trevor ran a Codex task on this machine, then read the
app before anything else: five-hour 51% remaining, so 49% used. The probe at 3:16 PM found two
sessions, 2:40 to 2:52 PM, 43 records, all the usual shape; the latest record, 2:52:49 PM, says 48%
used (weekly 19%, not read in the app). The app is one point ahead again, right after a turn with
nothing in between. Two comparisons in a row, both with known timing, show the same one-point gap
the same way round, which is what a log trailing by its last call would look like.

**2026-10-14, 7:52 PM run: 35 logs, 546 records.**

- **A third originator and a new version, same shape.** `codex-tui 0.162.1`, the terminal client,
  wrote one log with 63 records, 6:43 to 7:50 PM. All 524 records with figures, across three
  originators and two versions, share one shape. No new null records.
- **A fourth reset behaved.** Five-hour 48% at 2:52 PM, a reset due at 3:21 PM, then 0% at 3:27 PM
  with a later reset.
- **Comparison 4, about 7:52 PM.** App: five-hour 56% remaining, so 44% used, resetting 8:26.
  Latest record, 7:50:11 PM from the terminal client: 44% used, resetting 8:26:55 PM. Exact
  agreement on figure and reset. Weekly 26% used in the log, not read in the app.
- **Comparisons so far:** 1 one point the log's way (timing unknown), 2 and 3 one point the app's
  way (right after a turn), 4 exact. The gap is never more than one point and not always there.

## Result

**Closed 2026-10-14, after two days of the planned seven, at Trevor's call: plan on the log
route.** He uses Codex daily and wants the readout, and the evidence so far points one way.

Against the decision criteria:

| Row | Status at closing |
|---|---|
| One shape, comparisons agree, records in every active session | Met, with one qualification. One shape across all 524 records with figures, three originators, two versions. Every session with turns wrote records; the nulls came from the old version and from the app starting. Four comparisons: one exact, three one point apart, never more. |
| Codex ahead of the log with no local use between | Not shown. Comparisons 2 and 3 had the app one point ahead, but both right after a turn on this machine and with no use anywhere else; a log trailing by its last call fits, and comparison 4 was exact. The positive control, Codex used elsewhere, was never run. |
| Shape changes across versions or originators | Not seen. The only change is nulls, which a reader treats as no reading. |
| A window does not fall after its reset | Not seen. Four five-hour resets, all fell to 0%. |
| Codex used on fewer than three of seven days | Not tested; answered instead by Trevor's own use. |

What closing early leaves unproven, accepted as known gaps:
- **Usage elsewhere.** Codex used in the browser, on another machine or by cloud tasks writes no
  log here, so the figure stays low until the next turn on this machine. The readout states that
  it is as of the last Codex turn here, with its age.
- **The weekly reset**, due 2026-10-20, was never observed. The five-hour resets behaved, and
  constitution principle 6 already keeps a figure off screen once its reset has passed.
- **Future Codex versions** may change the record. A record the reader does not recognise is
  reported as unreadable, as the Claude reader does, never guessed at.
- **A one-point gap** right after a turn may be real. Not established either way.

For the reader that follows: a null `rate_limits` is no reading, never 0%; `resets_at` can differ
by seconds between records for one window, so it cannot be used as an exact key; the latest record
with figures wins, carrying its own timestamp as its age.

Consequence for the spec: AC-1 names the session logs. AC-3b, the remote icon, existed only for
readings from Codex's server, so it is deferred past 0.5.0 (Trevor's call); it returns if a later
feature reads that endpoint.
