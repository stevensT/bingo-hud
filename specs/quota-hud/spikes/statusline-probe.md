# Spike: status line probe

**Status:** open — started 2026-08-30, restarted 2026-09-01
**Script:** `scripts/quota-statusline.ps1`

## Question

Does continuous visibility of remaining quota actually change what I do, before running out
becomes urgent?

## Why this spike exists

The quota HUD spec rests on an assumption it does not test: that a glanceable number changes
behaviour. Everything else in the feature is engineering, and the engineering is sound. This
one assumption is not.

It matters because of where the cost sits. Phase 6 — the WPF shell, the frameless window,
click-through, edge snapping, all the P/Invoke — carries nearly all of the project's technical
risk and all of its Windows-specific difficulty, and it is built entirely on top of this
assumption. A number that is almost always green is a number people stop seeing; if that
happens here, the expensive part of the project is also the least load-bearing part of the
value, and it is much cheaper to learn that now.

## Method

`scripts/quota-statusline.ps1` renders one line into the Claude Code status line:

```
5h 12% resets in 53m · wk 37% resets Sat
```

It reads the existing OAuth token, calls the usage endpoint at most once every five minutes,
caches the parsed windows, and renders from cache on every other invocation. Colour turns
yellow at 25% remaining and red at 10%.

Run it for at least one full week, ideally spanning a weekly reset.

Running it creates a cache directory at `%LOCALAPPDATA%\bingo-probe\`. Only parsed percentages
and a timestamp are stored there — never the token, never the raw response body.

### Wiring it up

In `%USERPROFILE%\.claude\settings.json`:

```json
"statusLine": {
  "type": "command",
  "command": "powershell -NoProfile -ExecutionPolicy Bypass -File C:\\Users\\Trevo\\01_dev\\bingo-hud\\scripts\\quota-statusline.ps1"
}
```

`-NoProfile` matters — loading a PowerShell profile on every status line render is slow.

The path is absolute and machine-specific. Only one `statusLine` entry can exist, so installing
the probe displaces whatever was there; save the previous entry before overwriting it, because
the Exit step below puts it back.

After editing, run the command by hand once to confirm it renders. A `statusLine` that fails is
silent — Claude Code shows nothing rather than an error, which looks exactly like a probe that
is running and reporting nothing.

## What this tests

- Whether a continuously visible number gets looked at at all.
- Whether looking at it changes a decision — starting a task, stopping, or deferring one.
- The parsing path that Core will need: `limits[]` primary, flat keys as fallback, inversion
  from utilization to remaining, and a null `resets_at`.
- The honesty states, under real conditions rather than fixtures: sign-in, unreadable,
  unavailable, and stale.

## What this does not test

- **The always-on-top HUD.** The status line is only visible while Claude Code is on screen.
  Whether the number is wanted at other times is precisely what the HUD would answer, and this
  probe cannot answer it. It tests the value hypothesis sitting underneath both.
- Threshold notifications (AC-14 through AC-18).
- Click-through, edge snapping, dragging, the detail panel (AC-19 through AC-24).
- Server-reported severity (AC-6). The probe uses local thresholds only.
- The adaptive poll policy (AC-25). Fixed five-minute interval, two-minute floor.

## Deliberate deviations from the constitution

**Principle 1, test-first, is suspended for this spike.** The code is throwaway and its only
output is a decision, not a component. Writing tests for something built to be deleted would be
ceremony. The Core implementation that may follow gets tests first, without exception.

**Principle 6, never display an unbacked number, stays in force.** The probe shows an explicit
error state and no percentages when it cannot authenticate, cannot parse, or cannot refresh, and
it marks a reading's age once that reading is older than one refresh interval. That rule is not
suspended for convenience, because the habit it protects is the whole point of the product.

## Decision criteria

Record the outcome honestly, including the unflattering one:

| Observed | Reading | Consequence |
|---|---|---|
| Looked at it, and it changed at least one real decision | The value hypothesis holds | Proceed with the spec. The remaining HUD question narrows to: do I want this when Claude Code is not on screen? |
| Looked at it, but it never changed a decision | Interesting, not useful | The alerts likely carry the value. Consider cutting the HUD and shipping a notification-only tool. |
| Stopped noticing it within days | Dashboard blindness confirmed | Reconsider the readout premise before writing any WPF at all. |

## Exit

When the spike closes, record the result below, delete `scripts/quota-statusline.ps1`, and
restore the previous `statusLine` entry. It is committed so the experiment is reproducible,
not because it is a deliverable.

## Findings so far

**2026-08-30 — percentages read better as consumed than as remaining. Contradicts AC-2.**

The probe originally displayed remaining, per AC-2 and the plan's "Percentage direction"
decision, which reasoned that "the number a person acts on is what is left." Seeing it in
place, the owner preferred consumed, and the probe was changed to match.

The argument for consumed that the original decision missed: `/usage` reports consumed, so a
readout showing remaining forces a mental subtraction every time it is compared against the
tool it is meant to summarise. Consistency with the number already in the user's head may beat
the abstract argument about which figure is more actionable.

The argument for remaining still stands on its own terms, and all three prior-art tools invert
before display. Rather than pick a winner on one day's reaction, AC-2 was amended to make
consumed the default and the direction a user setting (AC-2a), which retires the question.

That amendment introduced a new one. A percentage whose direction is a setting the reader
cannot see can be read exactly backwards, so AC-2b now requires the figure to state which
direction it is in.

**The probe deliberately does not carry that label.** It renders a bare `41%`. That is a
divergence from AC-2b, and it is useful: a week of reading an unlabelled figure is direct
evidence for whether the label AC-2b mandates is genuinely load-bearing or merely cautious.
Record at the end of the week whether the bare number was ever misread — including the case
where it never was.

**2026-09-01 — the probe was not running, and the week has to restart.**

Moving to a new machine left the `statusLine` entry pointing elsewhere, and the install snippet
above named a user profile directory that does not exist here. Nothing errored, because a
failing status line renders as nothing at all. Two days were nominally collected on the previous
machine; treat the observation window as starting 2026-09-01.

The failure mode is worth keeping in mind beyond this spike: an absolute path in configuration
outside the repository is invisible to every check the project runs, and a silent renderer gives
back no signal that it has stopped.

## Result

_Open. Nothing to record yet._
