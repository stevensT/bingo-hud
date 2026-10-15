# Codex Quota — Technical Plan

## Technical Approach

Bingo gains a second source beside the Claude one, and the display goes from one reading to one
reading per product. The Claude path — credential file, usage endpoint, `QuotaMonitor`,
`PollLoop` — is not changed in what it does. The Codex path is new and entirely local:

```
%USERPROFILE%\.codex\sessions\**\*.jsonl ─┐
%USERPROFILE%\.codex\archived_sessions\  ─┤  (or under CODEX_HOME)
                                          ▼
                     CodexLogReader  — finds the newest record with figures
                          │            (record timestamps; text prefilter, then parse)
                          ▼
                     CodexRecord.Parse — one line → QuotaSnapshot, null record, or unreadable
                          │
                          ▼
                     CodexMonitor — holds the last reading and the last problem;
                          │         rescans on a 60 s timer and on manual refresh
                          ▼
  QuotaMonitor ──► Readout / PanelReadout / AlertEngine, now per product ──► HUD, panel, toasts
```

**Reading the logs.** A rate-limit record is a line whose `type` is `event_msg` and whose
`payload.type` is `token_count`. Every session log is inspected. A line is parsed only if it
contains the literal text `"rate_limits"`, and only its timestamp and rate-limit fields are
kept. Each log contributes its last complete record with figures or an unknown shape; null
records contribute nothing. Across logs, the newest record timestamp wins. An unreadable record
keeps its timestamp when available, so an older unknown shape cannot hide a newer valid reading;
without a readable timestamp, a broken record is conservatively reported as a problem.

Changed 2026-10-15 during task 5.6. The original shortcut ordered files by modification time and
stopped at the first reading, and the monitor skipped rescans when the newest file time stayed
unchanged. On Windows a live writer can append records without advancing that metadata. The
on-screen comparison selected 16% used while the account showed 25% used; the newer record was
in a file whose modification time looked older. The shortcut was removed rather than replaced
with another metadata guess. Full scans of the real logs took 115 to 167 ms, below the one-second
bar. The source is still local session logs, with the same accepted limits.

**Mapping the record.** `primary` and `secondary` are mapped by `window_minutes`, not by name:
300 becomes `WindowKind.Session` and 10080 `WindowKind.WeeklyAll`. Any other window length is
ignored and named in the panel, as an unknown key is for Claude. `used_percent` is stored as sent,
consumed 0 to 100, as for Claude. `resets_at` is Unix seconds. `ObservedAt` is the record's own
`timestamp`, not the moment Bingo read it, so the age on screen is the time since the last Codex
turn. A shape that does not match is unreadable; a null `rate_limits` is no reading.

**One reading per product.** `HudContent` becomes a list of product sections, each either a
reading (lines plus its own mark) or an empty phrase. `Readout` composes a section from a snapshot
and the facts about it; the Claude section is built from `ReadingState` exactly as today, and the
Codex section from `CodexState`. With only Claude shown the HUD is one section, as 0.4.0 draws it
apart from the label (AC-9).

**Codex states.** `CodexState` holds the last reading, the last problem, and whether Codex was
found. Its states, and what each shows:

| State | HUD | Severity colours |
|---|---|---|
| Not found (no `.codex` folder) | "Codex not found" in place of lines | — |
| Found, no record with figures yet | "No Codex reading yet" | — |
| Reading, logs read cleanly | lines, mark "used 12m ago" (AC-3, AC-3a idle) | kept: a true figure, only old |
| Reading, last scan failed (I/O, or a record in an unknown shape) | lines, mark "12m ago, logs unreadable" | dropped, as a frozen Claude reading's are (AC-3a failed) |
| A window's reset has passed since the reading | that line: window name and "reset, no use since"; no figure, no bar | excluded |

The last row is AC-3: the figure belongs to an occurrence that has ended, and the new one has no
reading, so no number is shown. Applied to Codex only. Claude polls at least every half hour and
AC-9 holds its display to 0.4.0's; the same rule for Claude is a separate change.

**Alerts.** `AlertKey` gains a product. Codex windows alert on the same thresholds and
once-per-occurrence rule (AC-10); a line whose reset has passed raises nothing. Because Codex's
`resets_at` moves by seconds between records for one window (spike result), a key counts as
already fired when a fired key for the same product, window and threshold resets within ten
minutes of it. Two occurrences of one window are at least five hours apart, so this cannot join
two of them.

**Products setting.** `UserSettings.Products` holds a switch per product, null until the first-start
question has been answered. On start with it null, Bingo looks for each product (Claude Code:
`%USERPROFILE%\.claude`; Codex: `%USERPROFILE%\.codex` or `CODEX_HOME`), shows a small window with
one tick box per product — found ones ticked, missing ones unticked and marked "not found" — and
saves the answer (AC-8a, AC-8b). Afterwards the tray menu has a check mark per product (AC-8c). At
least one stays on: the last ticked product's check mark cannot be cleared (AC-8).

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| How Codex is read | All local session logs; newest record timestamp wins; text prefilter before parsing | File modification times can lag a live writer. Corrected at task 5.6 on 2026-10-15; the prefilter remains the privacy line in AC-1. |
| Codex model | Reuse `QuotaSnapshot` and `QuotaWindow`; `RawBody` holds only the `rate_limits` object's text | The windows are the same shape as Claude's, so severity, bars, reset phrases and alerts apply unchanged. Principle 5: no Codex-specific window type until one is needed. |
| Codex monitor | A new, small `CodexMonitor`, not a second `QuotaMonitor` | `QuotaMonitor` is about credentials, HTTP outcomes, backoff and single-flight requests; none apply to reading a local file. Bending it to fit would put Codex cases into every one of its branches. |
| Abstraction over the two sources | None. Two concrete monitors, combined where the display is composed | Principle 5: two sources is not yet a pattern. The spec's non-goals rule out a general provider system. |
| Codex scan cadence | Every 60 s and on Refresh now; every requested scan reads | Unchanged file times do not prove unchanged records on Windows. Full scans measured 115–167 ms at task 5.6. |
| Window mapping | By `window_minutes` (300, 10080), not by `primary` and `secondary` | The names say nothing about length; the minutes do. A renamed or reordered window still lands right, and an unknown length is ignored rather than guessed into a slot. |
| Age of a Codex reading | The record's own timestamp | The figure is true as of the last turn, not as of Bingo reading it. Using the read time would make an hours-old figure look current. |
| A Codex window whose reset has passed | No figure, phrase "reset, no use since", no severity, no alert | AC-3 and principle 6: the old figure is the last occurrence's, and the server sent nothing for the new one. |
| `rate_limit_reached_type` | Shown verbatim in the panel when not null; does not change HUD severity | Never seen non-null in the spike, so what its values mean is unknown. Treating it as "limited" would be a guess on screen. Local thresholds still turn the line red near 100%. |
| Alert identity across `resets_at` jitter | Ten-minute tolerance when matching a fired key, for both products | Codex's reset moves by seconds between records; an exact key would alert twice for one crossing. Harmless for Claude, whose resets do not move. |
| Old `alerts.json` entries | Read with no product as Claude | 0.4.0 wrote no product. Reading them as Claude keeps every alert already shown from firing again after the upgrade. |
| Where collapse applies | Per product: each section collapses to its worst window, unless both of its windows need attention | The rule exists so a window about to run out is never hidden. Collapsing across products would let a Claude warning hide a Codex window running out. |
| HUD accent bar | One per product, beside that product's lines only, showing its worst live line | *Changed 2026-10-14 at task 5.4, from "worst live line across all shown products".* On screen, a single accent ran down beside both products and read as both being in trouble when only Claude was. The accent says which product needs attention, as well as that something does. |
| Product label | Every line, "Claude 5h", "Codex Week" (AC-7a) | Decided in the spec. The label column widens; the figures stay aligned. |
| First-start question | A small WPF window shown before the HUD, wording and detection decided in Core | Bingo's first dialog (spec Decided). Core decides what is found and whether the answer is valid, so it is tested; the window only draws it. |
| Detection | A folder test only: `.claude` and `.codex` (or `CODEX_HOME`) under the user profile | It answers "is it installed here" well enough to pre-tick a box the user then confirms. Running either program to ask would be slower and could prompt for sign-in. |
| Refresh now | Still refreshes Claude; also rescans the Codex logs at once | The Codex rescan is local and free. One button that refreshed only one of two products would read as broken. |

Each decision keeps to the constitution. One tension to note: the AC-3 reset rule is applied to
Codex only. Explicit over implicit argues for one rule for both; AC-9 argues for leaving Claude as
0.4.0 has it. AC-9 wins here, and a Claude line showing an ended window's figure for up to half an
hour is recorded as a candidate for a later change rather than fixed silently.

## Data Model

```
enum Product { Claude, Codex }                       // Claude first: old alert entries default to it

record CodexState(
    bool Found,                                      // .codex folder exists
    QuotaSnapshot? Last,                             // newest record with figures
    CodexProblem? Problem,                           // last scan's failure, or null
    string? ReachedType)                             // rate_limit_reached_type, verbatim

abstract record CodexProblem                         // why the last scan gave nothing usable
    LogsUnreadable(string Reason)                    // I/O failure, access denied
    UnknownShape(string Reason)                      // a record Bingo does not recognise

record ProductSwitches(bool Claude, bool Codex)      // at least one true
UserSettings + ProductSwitches? Products             // null = first-start question not answered

AlertKey + Product Product                           // default Claude when absent in the file

HudContent → HudContent(IReadOnlyList<ProductSection> Sections, Severity Overall)
ProductSection = Reading(Lines, Mark, Frozen) | Empty(Phrase), with its Product
ReadoutLine.Window → "Claude 5h", "Codex Week"; a reset-passed line carries Percent = null, Bar = null

PanelContent + one block per shown product: rows, exact resets, age, status; Codex adds the
"as of the last Codex turn on this machine" note (AC-1) and the reached type when present
```

`ReadingState`, `QuotaMonitor`, `PollLoop` and `UsageNormalizer` are unchanged.

## Interfaces

- **Codex log record** (input): `{"timestamp":"…","type":"event_msg","payload":{"type":"token_count",…,"rate_limits":{"limit_id":"codex","primary":{"used_percent":44.0,"window_minutes":300,"resets_at":1792…},"secondary":{…},…}}}`.
  `rate_limits` may be `null`.
- **Fixtures**: `tests/fixtures/codex/`, dated JSONL files cut from real logs and scrubbed to the
  record lines plus a few decoy conversation lines with invented text, so the prefilter is tested
  against what it must skip. A README there records where each came from, as the usage fixtures do.
- **`CodexLogReader`**: `CodexScan Scan()` over a root folder, returning the newest record by its own timestamp with
  figures, a null-only result, not found, or a problem. Takes the root, so tests run against a
  temp folder.
- **`CodexMonitor`**: `CodexState Current`, `void Rescan()`; driven by a 60 s timer in the shell
  and by "Refresh now".
- **`ProductDetection`**: `ProductSwitches Suggested(string userProfile, string? codexHome)` and
  whether each folder was found, for the first-start window.
- **Tray**: two new check items, "Show Claude" and "Show Codex", above the display options.
- **Notifications**: titles and bodies name the product, from `AlertMessage` (AC-10).

## Implementation Phases

1. **Fixtures and the record parser** — scrub fixtures from the real logs; `CodexRecord.Parse`
   test-first against them: figures, null, unknown shape, unknown window length, missing fields.
2. **Reading the logs** — `CodexLogReader` (record timestamps, null records passed over, prefilter, I/O
   failures as problems) and `CodexMonitor` (rescan only on change, idle vs failed), against temp
   folders built from the fixtures.
3. **Products in the model** — `Product`; `AlertKey` with product and the ten-minute match;
   `alerts.json` read with old entries as Claude; `UserSettings.Products` and its store
   round-trip; `ProductDetection`.
4. **Display in Core** — `Readout` per product (labels, per-product collapse, Codex marks, the
   reset-passed line); `StatusMessage` Codex words; `PanelReadout` per product; `AlertMessage`
   with product names; the AC-9 test that a Claude-only HUD matches 0.4.0's apart from the label.
5. **Shell** — first-start window; tray check marks with the last-one rule; HUD drawing sections;
   panel blocks; App wiring of `CodexMonitor`, its timer, alerts from both products.
6. **Docs and release** — README rewritten as a readout for AI coding quotas (spec Decided), with
   the Codex source and its limits in "How it works"; CHANGELOG; version 0.5.0.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| A Codex update changes the record or where logs live | The Codex line goes unreadable | It says so (AC-3a failed state) and shows no guessed figure; the fixtures pin the shape known today, so a fix starts from a failing test. |
| Large logs make each scan slow | Bingo uses noticeable disk and CPU every minute | Full scans measured 115–167 ms against the real logs at task 5.6. Keep the 60 s cadence; remeasure as logs grow before adding an optimization. |
| Codex used elsewhere is invisible | The Codex figure is lower than the truth until the next turn here | Accepted in the spike result. The panel says the reading is as of the last Codex turn on this machine (AC-1), and the mark always gives its age. |
| The one-point gap after a turn | The Codex figure can trail Codex's own by a point | Accepted in the spike result. The age mark tells the user when the figure is from. |
| `alerts.json` from 0.4.0 misread | Old alerts fire again after the upgrade, or the file is dropped | Old entries read as Claude, tested against a file in 0.4.0's exact shape. |
| Four lines and two marks make the HUD tall | It covers more of the screen | Per-product collapse still applies, and either product can be switched off. Checked on screen in phase 5. |
| The first-start window appears on every start | A question asked again is a bug the user sees daily | It appears only while `Products` is null, and saving the answer is tested; if the save fails, the answer is still used for the session. |
| Privacy: conversation text reaches memory or disk | The logs hold the user's conversations | Prefilter on `"rate_limits"` before parsing; only the rate-limit object is kept as `RawBody`; a test feeds decoy conversation lines and checks none of their text is held anywhere in the state. |
