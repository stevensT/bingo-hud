# Spike: the display options, on screen

**Status:** closed 2026-10-01
**Script:** `scripts/stub-usage-server.js`, restored from `f5027f7` with one mode added

## Question

Does the built shell draw the countdown and the bar the way the tests say Core composes them, in
every severity and in a frozen reading? The tests prove the strings and the segment fills; they
do not prove that a window shows the bar in the right colour, fits it, or keeps it on screen.

## Method

The Debug overrides from the quota-hud error-states spike: `BINGO_CREDENTIALS_PATH` to a fake
credential file in a scratch folder, and `BINGO_USAGE_ENDPOINT` to the stub on loopback. With
both set, the real token is never read and nothing reaches the real endpoint.

The stub is the one that spike used, with one mode added: `over`, the session window at 120%
used. Each state is captured from a Debug build launched fresh, so the first poll answers from
the chosen mode; frozen is a success followed by a 401 at the next poll. Settings are written to
`settings.json` before each launch. The trial instance is stopped and `%LOCALAPPDATA%\Bingo` is
backed up first, and both are restored afterwards.

| Capture | Mode | Settings |
|---|---|---|
| Defaults at warning | `warning` (session 80%) | clock time, no bar |
| Countdown only | `warning` | countdown, no bar |
| Bar only | `warning` | clock time, bar |
| Both, each severity | `ok`, `warning`, `critical`, `rejected` | countdown, bar |
| Over 100 | `over` (session 120%) | countdown, bar |
| Frozen | `critical`, then `401` at the next poll | countdown, bar |

## What this does not test

- **Stale on screen.** A stale reading needs 45 minutes of failed polls, and the display sleeps
  before then, which captures as black. Stale is assessed against tests only: nothing in the new
  code treats stale specially. The bar takes the line's severity, which is tested
  (`AStaleReadingKeepsItsSeverity`, `AReadingThatIsNotCurrentKeepsItsCountdownAndItsMark`), and the
  mark row was seen on screen at quota-hud 7.4.
- The real endpoint producing any of these, beyond the normal reading seen at task 3.3.
- Collapse on screen. Which lines exist is unchanged by the options and tested in all four
  combinations (AC-15); the shell draws whatever lines it is given.
- Release builds, which ignore the overrides by design.

## Deliberate deviations from the constitution

**Principle 1, test-first, is suspended for the stub server only.** It is throwaway, and its only
output is a set of photographs.

## Decision criteria

| Observed | Reading | Consequence |
|---|---|---|
| Every capture shows the expected text; bar and figure share a colour in every severity; partial segments visible; frozen bar uncoloured beside its mark; nothing off screen | AC-3, AC-7 to AC-13, AC-15 met on screen | Record as met at 4.5 |
| A bar coloured differently from its figure, a bar drawn with no reading, or the HUD off screen | A shell defect the tests cannot see | Fix before 4.5 passes; the photograph is the reproduction |
| A capture cannot be reached through the stub | The method is incomplete | Record which, and assess it against tests only, saying so |

## Exit

Record the result below, then delete `scripts/stub-usage-server.js` in the commit after the one
that records it, so the experiment stays reproducible from history.

## Result

**Closed 2026-10-01.** Every capture in the table was reached and seen, and every one met the
first row of the decision table. Stale was not captured, as stated above.

- **Defaults at warning:** `5h 80% used resets 4:07 PM` / `Week 37% used resets Sun 1:07 PM`,
  the figure amber with the amber accent. 0.1.0's HUD, unchanged (AC-16).
- **Countdown only:** `2.9h` and `2.9d` for resets about 3 hours and 3 days out. Rounded down,
  since a few seconds pass between the stub's answer and the render (AC-2).
- **Bar only:** the bar beside the clock-time phrase. The widest combination, 458 px, and still
  on screen at the right edge.
- **Each severity, both options on:** normal fills dimmed white; warning amber; critical red;
  rate-limited magenta with "limited, 2.9h". In every one the bar and the figure share their
  colour (AC-10). Partial segments show: 37% is three segments and seven tenths of the fourth,
  95% nine and a half, 12% one and a fifth (AC-8).
- **Over 100:** `120% used` beside a bar that is full and goes no further (AC-13).
- **Frozen:** a critical 95%, then a 401 at the next poll five minutes later. The bar and the
  figure dropped to uncoloured, the countdown stayed, and "5 min old, sign-in expired" appeared
  beneath the lines (AC-3, AC-11). The HUD grew upward and stayed on screen.

Two captures were first spoiled by a toast lying over the HUD. Not a defect: the stub moves its
reset times on every request, so each fresh launch is a new occurrence and fires the 80% alert
again, and Windows puts toasts where the HUD sits by default. Both were retaken after the toast
cleared.

The trial instance was stopped for the run and `%LOCALAPPDATA%\Bingo` backed up; both were
restored afterwards. The stub script is committed with this result and deleted in the commit
after, so the experiment stays reproducible from history.
