# Codex Quota

## Overview

Bingo shows how much of the Claude usage windows is left. People who also use OpenAI's Codex on a
ChatGPT plan are governed the same way there: a five-hour window and a weekly window, each with a
reset, and the same interruption when one runs out mid-task. Today the only way to check the
Codex side is inside Codex itself.

This feature adds the Codex windows to Bingo, read from a source that has been shown to agree with
what Codex itself reports, and drawn under the same honesty rules as the Claude windows: every
figure traceable to a reading the server produced, carrying its age, and labelled when it is not
current (constitution principle 6).

Which source that is has not been decided. Work starts with a spike,
[`spikes/session-log-probe.md`](spikes/session-log-probe.md), which tests whether the rate-limit
record Codex writes into its own session logs is sound enough to read. No plan is written until
it closes.

## User Stories

- As someone who uses both Claude Code and Codex, I want both products' windows in one glance, so
  that I can decide which one to work in before either runs out.
- As a Codex user, I want the Codex figure marked when it is not current, so that I never act on a
  number that stopped moving.
- As a Claude-only user, I want to be asked once which products to show, so that Bingo does not
  grow lines I have no use for.

## Decided

Recorded so they are not reopened:
- Bingo becomes a readout for AI coding quotas generally, with Claude and Codex as peers. The
  README is rewritten to match, and every line carries its product label (AC-7a).
- Products shown are chosen on first start and remembered, rather than defaulting on or off
  (AC-8a). The cost: Bingo gains its first question to the user, a window it has never had.
- A product that is shown but has no reading says so rather than disappearing (AC-7).
- Codex windows alert on the same rules as Claude windows (AC-10).
- Products found on the machine start ticked in the first-start question; each product then has a
  check mark in the tray menu (AC-8b, AC-8c).
- A Codex reading that stopped because Codex is idle looks different from one that stopped because
  a fetch failed (AC-3a).
- Readings from a remote source carry a remote icon; local readings carry none (AC-3b).
- Target release 0.4.0.

## Acceptance Criteria

### Source
- [ ] AC-1: The Codex source is the one the session log spike settles on. This criterion is
      rewritten once the spike closes, to name the source and what it can and cannot see.
- [ ] AC-2: Every Codex figure on screen comes from a reading Codex's server produced. Nothing is
      estimated, carried across a reset, or filled from a plan limit.
- [ ] AC-3: A Codex reading carries its age. A reading whose window has reset since it was taken
      is labelled as such and never shown as the new window's figure.
- [ ] AC-3a: A Codex reading that has stopped moving because Codex has not been used since is
      marked differently from a reading that is stale because a fetch failed. The first is a true
      figure that is simply old; the second is Bingo failing to learn the current one, and the
      reader acts differently on each. Exact wording is settled in the plan.
- [ ] AC-3b: A reading taken on this machine carries no mark. A reading fetched from a remote
      source carries a remote icon, so the reader can tell a figure from Codex's own logs here
      from one the server reported for the whole account.
      [Assumed, to confirm: the icon applies to Codex only. Bingo can tell where a reading came
      from, not where the usage behind it happened, so the icon marks the source of the reading.
      Every Claude reading comes from the server, so under the same rule every Claude line would
      carry the icon permanently, which tells the reader nothing.]
      [Depends on the spike: a remote Codex reading exists only if Codex is also read from its
      endpoint. If the session logs prove sufficient on their own, Codex has no remote readings
      and this criterion is dropped.]

### Display
- [ ] AC-4: Each Codex line reads like a Claude line — percentage with its direction word, reset
      phrase, bar when the bar is on — and is labelled with its product, so a Codex figure can
      never be read as a Claude one.
- [ ] AC-5: The Codex lines follow the existing display settings (direction, reset format, bar)
      rather than having their own.
- [ ] AC-6: The detail panel shows the Codex windows with their exact reset times and the age of
      the reading.
- [ ] AC-7: When Codex is shown but is not installed, or has written no reading yet, the Codex line
      says why and shows no figure. It is never hidden, because a missing line looks the same as
      Bingo failing to look.
- [ ] AC-7a: Every line carries its product label, Claude lines included. Bingo is a readout for
      AI coding quotas, not a Claude readout with an add-on, so neither product is the unlabelled
      default.

### Settings
- [ ] AC-8: Which products are shown is a user setting, one switch per product, changed where the
      existing display settings are and persisted like them. At least one product is always on.
- [ ] AC-8a: The first time Bingo starts without that setting saved, it asks which products to
      show, and remembers the answer. This covers a new install and the first start after
      upgrading from 0.3.0. Bingo does not ask again; the answer is changed afterwards through the
      setting.
- [ ] AC-8b: Before asking, Bingo looks for Claude Code and Codex on the machine. Each one found
      starts ticked; the user unticks any they do not want before continuing. A product not found
      starts unticked and is marked as not found, but can still be ticked, since it may be
      installed later.
- [ ] AC-8c: After setup, each product has a check mark in the tray menu beside the existing
      display settings, which turns it on or off.
- [ ] AC-9: With only Claude shown, the HUD reads as 0.3.0 does apart from the product label
      (AC-7a).

### Alerts
- [ ] AC-10: Codex windows raise threshold alerts under the same thresholds and once-per-window
      rule as Claude windows. A Codex alert and a Claude alert for the same kind of window are
      independent, and every notification names its product.

## Non-Goals

- **Any product other than Claude and Codex.** No general provider system; a third product would be
  its own feature.
- **API-key or pay-per-token Codex usage.** This is about subscription windows, as Bingo is for
  Claude.
- **Codex credits or balances.** The record carries a credits field; it is not shown.
- **Combining the two products into one figure**, or comparing them.
- **Projection or burn rate**, for the same reasons it is a quota-hud non-goal.
- **Multiple Codex accounts.**
- **Signing in to Codex, or refreshing its token.** Bingo never takes over anything Codex manages.

## Open Questions

- Whether the remote icon (AC-3b) applies to Codex only. Assumed so; to confirm.
- The source, and with it whether "nothing leaves your machine" still holds: settled by the spike.

## Dependencies

- The session log spike, which blocks the plan.
- Codex installed and signed in on a ChatGPT plan.
- quota-hud and hud-display-options: the reading model, display settings, and alert rules this
  feature extends rather than duplicates.
