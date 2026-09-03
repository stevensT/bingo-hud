# Spike: click-through probe

**Status:** closed — started and answered 2026-09-03. Code deleted in the commit after the one that recorded it.
**Code:** `scripts/click-through-probe/` (a two-file console app; run with
`dotnet run --project scripts/click-through-probe`)
**Timebox:** one session. If the answer is not in hand by the end of it, that is itself the
result: neither mechanism works cleanly, and AC-21 is amended before any HUD code is written.

## Question

Can a window be click-through and clickable in the same lifetime? Concretely: can
`WS_EX_TRANSPARENT` be removed and restored on a running WPF window, with the change taking
effect on the next click, and can the app find out that the cursor has arrived over the window
while the window itself is receiving no mouse input?

## Why this spike exists

AC-21 says the HUD is click-through when idle. The plan's risk table says hit-testing toggles
back on when the cursor hovers, so the panel stays reachable. Those two rest on a premise task
6.1 names outright: a window carrying `WS_EX_TRANSPARENT` gets no mouse messages, so WPF's
`MouseEnter` never fires, and the hover that is supposed to switch hit-testing back on is
invisible to the thing that needs to see it. Something outside the window has to watch the
cursor. The two candidates are a timer polling the cursor position, and a low-level mouse hook.

The timer is tried first because it is the boring one: a `DispatcherTimer`, `GetCursorPos`,
`GetWindowRect`, and a rectangle test. A low-level hook (`WH_MOUSE_LL`) sees every mouse
event on the system, runs on the UI thread's message loop, adds latency to all input while its
callback runs, and is the kind of thing security software notices. It is only worth its cost
if the timer proves unable to do the job.

## A premise this spike surfaces but does not settle

"Click-through when idle, clickable on hover" contains a tension the spec has not named. A
click meant for what is beneath the HUD is made with the cursor over the HUD — which is exactly
the moment hover has switched hit-testing back on. Taken literally, the hover rule makes the
HUD never click-through when someone is actually trying to click through it.

The mechanism this spike tests does not depend on how that is resolved. A cursor timer can
implement hover, dwell (clickable only after the cursor has rested for some time), or a
modifier key (`GetAsyncKeyState` in the same tick) with equal ease. The policy is a design
question for task 6.5 and possibly an amendment to AC-21; it is recorded here so it is not lost,
and so the result below is not read as settling it.

## Method

Two WPF windows, built in code so the probe stays at two files:

- **Under**: an ordinary window that records every left-button press it receives.
- **HUD**: frameless, topmost, `AllowsTransparency`, with a visibly opaque background, placed
  exactly over Under. It also records presses.

The app drives the cursor itself with `SetCursorPos` and clicks with `SendInput`, so the
result does not depend on a person's hand and can be re-run. Four steps, each ending in a
click at the centre of HUD and a record of which window received it:

| Step | HUD ex-style | Cursor | Expected receiver |
|---|---|---|---|
| a | `WS_EX_TRANSPARENT` set | over HUD, held still | Under |
| b | `WS_EX_TRANSPARENT` removed at runtime | unchanged | HUD |
| c | `WS_EX_TRANSPARENT` restored at runtime | unchanged | Under |
| d | left to a 50 ms cursor timer | moved out, then in, then out | HUD while in; ex-style reads transparent again after leaving |

Steps a to c isolate the runtime toggle from any policy: the cursor never moves, so only the
style change can explain a different receiver. Step d is the actual claim of task 6.1: a
polling timer, with no mouse input reaching the window, observes the enter and the leave and
drives both transitions.

The probe prints one line per step and exits 0 only if every step matched. That exit code is the
spike's runnable check.

The mouse is taken over for about three seconds while it runs. Hands off during that time.

## What this does not test

- **Per-pixel transparency.** A layered window already passes clicks through pixels whose
  alpha is zero. HUD's background is deliberately opaque over the click point so that this
  cannot masquerade as the ex-style working.
- **A non-layered window.** `AllowsTransparency` sets `WS_EX_LAYERED`, and
  `WS_EX_TRANSPARENT` is documented to pass mouse input to other processes only in combination
  with it. The HUD will be layered, so the untested case does not matter here.
- **The low-level hook.** It is the fallback, built only if the timer fails a step.
- **Real hands.** Synthetic input via `SendInput` is hit-tested like real input, but a person's
  cursor crossing the edge at speed, or resting on it, is not exercised. Task 6.5's enter and
  leave coverage is where that belongs.
- **DPI.** Every coordinate the probe uses comes from `GetWindowRect` and `GetCursorPos`, both
  in physical pixels, so no DIP conversion is exercised. The real HUD will need it for
  persistence of position (6.2), not for this mechanism.
- **The policy question above.** Hover, dwell, or modifier is not decided by this.

## Deliberate deviations from the constitution

Test-first is suspended. The output is a decision about which Win32 mechanism to build on,
not a component that ships; the probe is deleted when the spike closes. The probe is still
self-checking — expected receiver per step, exit code — because a spike whose result is read
off a screen by eye can be rationalised, and this one is not allowed to be. Principle 6 is not
touched: nothing here displays a number.

## Decision criteria

Written before the run.

| Observed | Reading | Consequence |
|---|---|---|
| a, b, c and d all match | Runtime toggling works and a cursor timer is enough to drive it | 6.5 builds on a `DispatcherTimer` polling `GetCursorPos`. No hook. The policy question goes to 6.5. |
| a, b, c match; d fails | The toggle works but polling misses the transition | Try `WH_MOUSE_LL`. If that also fails, amend AC-21. |
| b or c fails | The ex-style cannot be changed live, or not without a frame-changing `SetWindowPos` the probe does not do | Add `SetWindowPos` with `SWP_FRAMECHANGED` and re-run once. If still failing, AC-21 is amended: click-through becomes a fixed setting rather than a live toggle. |
| a fails | Clicks do not pass through at all | The premise of AC-21 is wrong for a WPF layered window. Amend AC-21 before anything else. |

## Exit

When the spike closes, record the result below, delete `scripts/click-through-probe/`, and
carry the chosen mechanism into task 6.5's description. The probe is committed so the
experiment is reproducible, not because it is a deliverable.

## Result

**2026-09-03 — first row of the table. A cursor timer is enough; no hook.**

Two consecutive runs, both `ALL PASS`, exit 0:

```
a: expected Under, got Under PASS
b: expected HUD, got HUD PASS
c: expected Under, got Under PASS
d: transparent-when-out=True clear-when-in=True click→HUD transparent-after-leave=True PASS
```

What that establishes:

- `WS_EX_TRANSPARENT` can be set and cleared on a shown WPF layered window with a bare
  `SetWindowLongPtr`, and the next click respects the new state. No `SetWindowPos`, no
  `SWP_FRAMECHANGED`, no re-show. Both directions, with the cursor held still, so nothing but
  the style change explains the receiver changing.
- A `DispatcherTimer` at 50 ms reading `GetCursorPos` against `GetWindowRect` sees the cursor
  arrive and leave while the window itself receives no mouse input, and drives both
  transitions. Task 6.1's question — timer or hook — is answered: timer.

Carried into 6.5: build on a `DispatcherTimer` polling `GetCursorPos`; the P/Invoke surface is
`GetWindowLongPtr`, `SetWindowLongPtr`, `GetCursorPos`, `GetWindowRect`, all of which belong in
the single interop file. The low-level hook was never built and there is no reason to.

Not settled here, and now owed by 6.5 before it writes code: the policy question above. The
mechanism supports hover, dwell, and a modifier key equally; which one AC-21 actually means is
a design choice, and the literal hover reading defeats the criterion's own purpose.

Settled at 6.5, 2026-09-03: dwell. AC-21 was amended to say so, `DwellPolicy` in Core holds the
rule, and the shell's 50 ms cursor timer feeds it. Verified on the built app by reading the
ex-style back: click-through on arrival, solid after 600 ms at rest, click-through again on
leaving, and still click-through after a 91 ms crossing.