# HUD Display Options

## Overview

The 0.1.0 HUD is words only: a percentage per window and a reset phrase. A week of daily use
showed it works and is accurate, but it is slower to read at a glance than it needs to be. Two
things cost the most. The reset phrase is a clock time ("resets Wed 1:19 AM") until the last
hour, which makes the reader do date arithmetic to answer "how long have I got". And the
percentage is a number to read, not a shape to see.

This feature adds two display settings, both off by default so 0.1.0's HUD is unchanged until the
user chooses otherwise. One shows the reset as a short countdown at every distance ("2h", "4d").
The other draws a segmented bar beside each window's percentage, so the state of each window can
be taken in without reading. Neither setting changes what Bingo knows, only how it is drawn.
Every figure still comes from a reading the server sent (constitution principle 6), and the exact
figures remain in the detail panel. Target release: 0.2.0.

The visual reference is the taskbar widget in CodeZeno/Claude-Code-Usage-Monitor
(`.github/animation.gif`): a line per window reading `5h [bar] 75% · 2h`, the bar ten segments
long and filled to the exact percentage, with the last segment partly lit.

## User Stories

- As a Claude Code user glancing at the HUD mid-task, I want the reset shown as time left, so
  that I know how long I have without working it out from a clock time and a weekday.
- As a user who prefers a visual, I want a bar beside each window's figure, so that I can see how
  full it is before I read the number.
- As a user who liked 0.1.0, I want both changes to be settings that start off, so that nothing
  moves until I ask it to.

## Acceptance Criteria

### Reset format
- [ ] AC-1: Reset format is a user setting with two choices: **clock time** (the 0.1.0 behaviour,
      a clock time when distant and a countdown inside the last hour) and **countdown**. Default
      is clock time.
- [ ] AC-2: A countdown is one unit, the largest that is at least one whole: days at a day or
      more ("4.5d"), hours at an hour or more ("2.3h"), minutes inside the last hour ("53m"). Days
      and hours always carry one decimal, rounded down to the tenth, so "2.0h" never shortens to
      "2h" and the line does not change width as the figure moves; "4.9d" covers 4d 21h 36m up to
      the next tenth. Minutes are whole and rounded down. The decimal mark follows the user's
      locale.
- [ ] AC-3: A countdown beside a reading that is not current is never left unlabelled. A stale or
      frozen reading keeps its countdown and carries the same status mark it carries in
      clock-time mode, as 0.1.0 does with its reset phrase (constitution principle 6: a figure
      labelled with why it is not moving is honest). The reset time is a fact the server sent,
      so the countdown to it stays true while the percentage ages.
      [Corrected while planning: the first draft said the mark replaces the countdown, which is
      not what 0.1.0 does. 0.1.0 keeps the reset phrase on each line and adds the mark as a row
      beneath them.]
- [ ] AC-4: A window the server reports with no reset time shows no countdown, in either mode.
      Nothing is guessed.
- [ ] AC-5: The detail panel continues to show exact reset times regardless of this setting. The
      panel is where a reset is checked against a clock.

### Bar
- [ ] AC-6: The bar is a user setting, on or off. Default is off.
- [ ] AC-7: When on, each window's line carries a bar between its label and its percentage. The
      label, the percentage with its direction word (quota-hud AC-2b), and the reset phrase all
      stay.
- [ ] AC-8: The bar is ten equal segments. Its fill length is the exact utilization the server
      reported, not rounded to a segment; segments are tick marks, so 75% lights seven segments
      and half of the eighth.
- [ ] AC-9: The bar always fills with usage consumed, whatever the consumed/remaining setting. That
      setting governs the wording of the figure beside it, which still states its direction.
- [ ] AC-10: The fill takes its window's severity colour in the same three discrete steps as the
      text (quota-hud AC-4): normal, warning, critical. Rate-limited remains distinct (quota-hud
      AC-6). Colour never shades continuously. A normal fill is Claude orange (`#D97757`) while
      the reading is live.
      [Amended 2026-10-01 for 0.3.0, after 0.2.0 shipped with a normal fill in the labels' dimmed
      white. Trevor's call: a bar that is working normally should look like Claude. Accepted risk:
      the orange sits between warning amber and critical red, so a normal bar may read as a
      warning in peripheral vision; if it does in use, the warning and critical colours are what
      move, not the orange.]
- [ ] AC-11: A stale or frozen reading keeps its bar, coloured exactly as the figure beside it is
      (so a frozen reading takes no severity colour, quota-hud AC-13), with the same status mark
      it carries without the bar, so it cannot be read as current. The bar and its figure never
      differ in colour, except at normal, where a live bar is orange beside a white figure and a
      frozen bar falls back to the labels' dimmed white. Orange means live, so a frozen bar never
      wears it.
      [Corrected while planning: the first draft took colour away from a stale bar too, but in
      0.1.0 a stale figure keeps its severity colour and only a frozen one loses it. Making the bar
      follow its figure keeps the two from disagreeing.]
- [ ] AC-12: With no reading yet, or an error state and no earlier reading, no bar is drawn; the
      HUD shows the same words it shows with the bar off. An empty bar means 0% and is never used
      to stand for "unknown".
- [ ] AC-13: A utilization above 100 fills the bar completely and no further; the figure beside it
      shows what the server sent.

### Both
- [ ] AC-14: Both settings are changed where the existing display settings are, and persist across
      restarts like them (quota-hud AC-22).
- [ ] AC-15: The two settings are independent: every combination renders correctly, and collapse
      (quota-hud AC-7) behaves the same in all four.
- [ ] AC-16: With both settings at their defaults, the HUD renders exactly as 0.1.0 does.

## Non-Goals

- **A bar that replaces the percentage.** The bar sits beside the figure; it never stands alone.
- **A continuous colour gradient.** Severity stays three discrete steps (quota-hud AC-4).
- **A segment count other than ten, or a user-chosen one.**
- **Any change to what is polled, how often, or how readings age.** This is drawing only.
- **Projection of any kind**, including a bar segment or countdown that anticipates where usage
  is heading. Burn-rate projection is a quota-hud non-goal and stays one.
- **A tray badge showing the percentage**, as the reference widget has. Separate idea, separate
  feature if wanted.
- **Renaming the severity states** (the deferred joker/bingo/winchester question in quota-hud).
- **Themes, fonts, sizes, or custom colours.**

## Open Questions

None open. Decided, and recorded so they are not reopened:
- One-unit countdown over two units, with a tenth for precision: "2.3h" rather than "2h 18m".
  Tenths of an hour are decimal, not minutes ("2.5h" is 2h 30m, not 2h 50m); accepted for the
  compactness, with exact times in the panel. The countdown carries no "left", which also removes the collision with a "% left"
  figure that AC-2b would otherwise have to referee.
- Partial fill over whole segments: the bar is as precise as the number beside it, so the two can
  never disagree and no rounding rule is needed.

## Dependencies

- **quota-hud (0.1.0)** — this feature changes how its readout is drawn and must keep every one of
  its honesty criteria (AC-8 to AC-13) true in every combination of the new settings.
- **The `v0.1.0` tag** — this is 0.2.0 work and starts after it.
