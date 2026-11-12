# Late Monitor Placement

## Overview

Bingo remembers where the HUD was dropped and puts it back there when it starts. On a desk with
three monitors, the HUD was kept at the right edge of a portrait monitor to the left of the main
one. Every morning after sign-in it came back at the left edge of the main monitor instead, at the
same height, and had to be dragged back. Quitting and relaunching by hand put it back correctly.

Bingo starts from the Startup folder early in sign-in. The likely cause is that the portrait
monitor is not connected yet at that moment, while the main monitor and a strip display further
left already are. The saved spot still falls inside the box drawn around all connected monitors,
so Bingo accepts it, then keeps the HUD within the nearest monitor, which is the main one. When
the portrait monitor connects a few seconds later, nothing looks at the placement again, so the
HUD stays where startup left it. The saved position itself is never lost: a drop is written to
disk at once, and startup's move is not saved.

This feature makes Bingo place the HUD again whenever the monitor setup changes, so the HUD
returns to its saved spot once that spot's monitor is connected. It also covers the drop-to-save
path with tests, which it has never had, and makes it visible when the HUD is not at its saved
spot because that spot is not on any connected monitor.

The late monitor is inferred, not observed: nothing recorded the monitor layout at sign-in. The
layout of the three monitors and the HUD's exact landing spot fit it, and no other layout found
would put the HUD there. The fix does not depend on which monitor is late or why.

## User Stories

- As a Bingo user with several monitors, I want the HUD back where I left it after I sign in,
  even when one of my monitors connects after Bingo has started.
- As a Bingo user, I want the HUD moved onto a monitor I can see when the monitor it is on goes
  away, and moved back when that monitor returns, without my saved spot being changed by either.
- As a Bingo user, I want to be able to tell when the HUD is somewhere other than where I left it
  because Bingo cannot find that spot's monitor, so that a misplaced HUD is explained rather than
  mysterious.
- As the maintainer, I want the drop-to-save path tested, so that a change that stops a drag being
  remembered fails a test rather than a morning.

## Acceptance Criteria

- [ ] AC-1: When the monitor setup changes while Bingo is running, Bingo places the HUD again
      from the saved position by the same rules it uses at startup. A monitor connecting,
      disconnecting, or changing resolution or arrangement each counts as a change.
- [ ] AC-2: After such a change, when the saved spot is on a connected monitor again, the HUD
      comes back to it, snapped to an edge as at startup. On the reported desk, the HUD moved to
      the main monitor at sign-in returns to the portrait monitor's right edge once that monitor
      connects, with no drag.
- [ ] AC-3: Placing the HUD at startup or after a monitor change never changes the saved position.
      Only a drop does. A HUD moved onto another monitor because its own is missing keeps waiting
      to go back to the saved spot for as long as Bingo runs.
- [ ] AC-4: A drag while the HUD is away from its saved spot saves the new drop, as any drag does,
      and later monitor changes place the HUD from that new spot.
- [ ] AC-5: The position saved after a drag is the drop position after it has been snapped to an
      edge, and it is written to the settings file when the button is released, without waiting
      for Bingo to close. Both are covered by tests.
- [ ] AC-6: While the HUD is not at its saved spot because that spot is on no connected monitor,
      the detail panel says so in one line, beside where the settings-save notice appears. The
      line goes once the HUD is back at its saved spot or the user drops it somewhere new. No
      file is written for it.
- [ ] AC-7: A monitor change that leaves the saved spot on a connected monitor does not move a HUD
      that is already there, beyond the edge snap and keep-on-screen adjustments that happen
      today.
- [ ] AC-8: The changelog's Unreleased section records the fix.

## Non-Goals

- **Remembering which monitor the HUD was on.** The saved position stays a pair of desktop
  coordinates. A monitor rearranged in Windows display settings can make those coordinates point
  somewhere else; that is accepted.
- **Changing where the HUD goes when its saved spot is unavailable.** Startup's existing choice,
  the nearest monitor or the top right of the main monitor, stays as it is. This feature only
  adds going back.
- **Saving on exit**, or saving the automatic keep-on-screen moves.
- **Mixed-DPI placement.** The existing `deferred:` note on per-monitor DPI stands. All monitors on
  the reported desk are at the same scale.
- **Changes to edge snapping or to how the HUD is dragged.**
- **Cutting the release.** Trevor has set this as 0.6.1, a patch: it changes no source reading.
  The version bump and the README's Releasing steps follow once this feature closes.

## Open Questions

None. Trevor decided on 2026-11-11 that AC-6 is a line in the detail panel, rather than a log
file or nothing.

One observation stays unexplained. The settings file was written at 5:05:15 PM on 2026-11-11,
seven seconds after a winget install restarted Bingo, and Trevor does not recall a drag or a tray
change then. The code has three writers of the settings file: a drop, the first-start answer, and
the tray menu. None runs at startup on its own. AC-3 pins that placement never saves, so a hidden
startup write would fail a test rather than stay a guess.

## Dependencies

- `specs/quota-hud/spec.md`, the criteria on dragging, edge snapping and remembering the HUD's
  position, which this must not regress.
- Windows reporting monitor changes to a running application.
