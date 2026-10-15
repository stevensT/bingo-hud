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

The source is the rate-limit record Codex writes into its own session logs, settled by the spike
[`spikes/session-log-probe.md`](spikes/session-log-probe.md), closed early on 2026-10-14. Bingo
makes no network call for Codex, so "nothing leaves your machine" still holds.

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
- Codex readings from a remote source carry a remote icon; local readings carry none. Claude
  lines never carry it (AC-3b). Deferred past 0.5.0 on 2026-10-14: with the session logs as the
  only source, there are no remote Codex readings to mark.
- A Codex line's bar, while live and normal, is drawn in Codex's blue rather than Claude's
  orange, so the two products are told apart at a glance as well as by label. The warning,
  critical and rate-limited colours are the same for both, because they say how much trouble a
  window is in. Decided 2026-10-14 by Trevor, on seeing both sections on screen (task 5.3); a
  narrowing of AC-4's "reads like a Claude line".
- The Codex source is its session logs (AC-1), settled 2026-10-14 when the spike closed early at
  Trevor's call, with its known gaps accepted (spike Result).
- Target release 0.5.0. First set at 0.4.0, and moved on 2026-10-13 when the 0.3.0 review fixes
  took 0.4.0: one of them changes how the usage response is parsed, which the versioning policy
  makes at least MINOR.

## Acceptance Criteria

### Source
- [ ] AC-1: Codex windows are read from the rate-limit record Codex writes into its session logs
      (`sessions\` and `archived_sessions\` under `%USERPROFILE%\.codex`, or under `CODEX_HOME`
      when it is set). Bingo makes no network call for Codex and does not read Codex's token.
      - The latest record with figures is the reading, and its own timestamp gives its age. A
        record whose rate limits are null is no reading, never 0%.
      - Only the lines that carry the rate-limit record or the session header are parsed, and
        only their rate-limit fields are kept. The logs hold the user's conversations; none of it
        is read further, kept, or written anywhere.
      - A record in a shape Bingo does not recognise is reported as unreadable, never guessed at.
      - What it cannot see: Codex used elsewhere (the browser, another machine, cloud tasks)
        writes no log here, so its usage appears only after the next Codex turn on this machine.
        The detail panel says the reading is as of the last Codex turn on this machine.
- [ ] AC-2: Every Codex figure on screen comes from a reading Codex's server produced. Nothing is
      estimated, carried across a reset, or filled from a plan limit.
- [ ] AC-3: A Codex reading carries its age. A reading whose window has reset since it was taken
      is labelled as such and never shown as the new window's figure.
- [ ] AC-3a: A Codex reading that has stopped moving because Codex has not been used since is
      marked differently from a reading that is stale because a fetch failed. The first is a true
      figure that is simply old; the second is Bingo failing to learn the current one, and the
      reader acts differently on each. Exact wording is settled in the plan.
- [ ] AC-3b: *Deferred past 0.5.0 (2026-10-14). Kept as written for the feature that reads Codex's
      endpoint, if one follows; not built or tested in 0.5.0.*
      A reading taken on this machine carries no mark. A reading fetched from a remote
      source carries a remote icon, so the reader can tell a figure from Codex's own logs here
      from one the server reported for the whole account.
      The icon is Codex-only. Bingo can tell where a reading came from, not where the usage
      behind it happened, so the icon marks the source of the reading, and only Codex has two
      possible sources. Every Claude reading comes from the server and is already the
      account-wide figure, so on Claude lines the icon would be permanent, would carry no
      information, and would teach the reader to ignore it where it does mean something. The
      cost: an unmarked line means "this machine's logs" for Codex and "the server" for Claude.
      That is accepted, because what protects the reader from acting on an old figure is the
      staleness marking (AC-3a and the existing Claude marks), not the icon.
      [A remote Codex reading exists only if Codex is also read from its endpoint. The spike
      settled on the session logs alone, so 0.5.0 has no remote readings.]

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
      upgrading from an earlier version. Bingo does not ask again; the answer is changed afterwards through the
      setting.
- [ ] AC-8b: Before asking, Bingo looks for Claude Code and Codex on the machine. Each one found
      starts ticked; the user unticks any they do not want before continuing. A product not found
      starts unticked and is marked as not found, but can still be ticked, since it may be
      installed later.
- [ ] AC-8c: After setup, each product has a check mark in the tray menu beside the existing
      display settings, which turns it on or off.
- [ ] AC-9: With only Claude shown, the HUD reads as 0.4.0 does apart from the product label
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

None. The source was the last one, settled when the spike closed on 2026-10-14.

## Dependencies

- Codex installed and signed in on a ChatGPT plan, and used on this machine: the logs are written
  only by Codex running here.
- quota-hud and hud-display-options: the reading model, display settings, and alert rules this
  feature extends rather than duplicates.
