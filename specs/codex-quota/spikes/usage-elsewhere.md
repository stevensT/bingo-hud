# Spike: Codex usage from elsewhere

**Status:** open, 2026-10-28
**Script:** none. The probe is Bingo's own readout and the Codex app, compared by hand.

## Question

When Codex is used somewhere other than this machine, how wrong is this machine's Codex reading,
and for how long? Specifically: does the next Codex turn here write a record that already includes
the usage from elsewhere, or does the log stay behind?

## Why this spike exists

AC-3b, the remote icon, only means something if Bingo also reads Codex's endpoint. The session log
probe planned that endpoint spike for one case: the logs missing usage from elsewhere
(`session-log-probe.md`, decision criteria, second row). That case was never tested; the probe
closed with the positive control unrun (its Result, second row).

Building the endpoint reader is expensive and reverses settled choices. Bingo would read Codex's
token and make a network call for Codex, which AC-1 rules out and the README's "nothing leaves your
machine" would no longer cover. It should be built only if the gap is real and large enough to
mislead.

Trevor has two machines, each running Bingo against its own logs, so the gap can be produced on
purpose.

## Method

Codex on the laptop is not used from step 1 until step 4, so any change it shows came from
elsewhere.

1. **Baseline, on the laptop.** Note the time, Bingo's Codex five-hour and weekly figures with
   their age (detail panel), and what the Codex app shows. They should agree within a point, as in
   the session log probe.
2. **Use elsewhere, on the desktop.** Use Codex enough to move the five-hour figure by at least
   five points. Note the desktop's own figure when done.
3. **Before a turn, on the laptop.** Note Bingo's figures and age again, and what the Codex app
   shows without sending anything. Bingo is expected not to have moved.
4. **One turn, on the laptop.** Send one short Codex turn. Note Bingo's figures once the new
   record lands, and the Codex app's figure.
5. **Repeat** steps 1 to 4 once more on another day, and once with the browser or a cloud task as
   the "elsewhere" if Trevor uses either.

Record each run under Findings: times, the four sets of figures, and the size of the gap.

## What this does not test

- **The endpoint.** Whether it is reachable, what it returns, and what calling it with Codex's
  token would risk. That is the next spike, if this one calls for it.
- **Other people's habits.** How often others use Codex away from the machine running Bingo.
  Trevor's own pattern stands in for that.
- **Accounts other than this one**, API-key sign-in, or other plans.

## Deliberate deviations from the constitution

**Principle 1, test-first, is suspended.** No code is written; the output is a decision.

**Principle 6 stays in force.** It governs what Bingo already shows, and this spike changes none
of it.

## Decision criteria

| Observed | Reading | Consequence |
|---|---|---|
| Step 4's record matches the account figure (desktop's figure plus the one turn, within a point) | The gap lasts only until the next turn here, and the age already shows it | Logs suffice. Close AC-3b as not needed; remove it from the spec and say why. |
| Step 4's record matches, but Trevor regularly uses Codex for hours away from a machine running Bingo, and then acts on a figure that is that far behind | The gap is bounded but large in real use | Open the endpoint spike. AC-3b returns with it. |
| Step 4's record does not include the usage from elsewhere | The log figure is not the account figure, so AC-2's premise is wrong | Stop. Raise it as a defect in 0.5.0 before anything else, then open the endpoint spike. |
| Step 3 shows the Codex app updated without a turn | Codex refreshes from the server between turns, so an account-wide figure is reachable without Bingo sending anything | Recorded as evidence for the endpoint spike, not a decision on its own. |

## Findings

### Run 1, 2026-11-03

**Step 1, baseline, laptop, 21:57.** Bingo was not running and was started at 21:57 (installed 0.5.0).
- Bingo, Codex: five-hour 77%, weekly 14%, reading 31 minutes old (last laptop record about 21:26).
- Bingo, Claude, for reference: five-hour 2%, weekly 5%, polled 21:57.
- Codex app: five-hour 22% left (78% used), weekly 86% left (14% used).
- Agreement: within a point on both (five-hour 77 vs 78, weekly 14 vs 14). Baseline holds.

**Step 2, use elsewhere, desktop, reported 22:14.** Desktop figures after use: five-hour 82% used, weekly 15% used. A move of 5 points from Bingo's baseline (4 from the app's), at the threshold.

**Step 3, before a turn, laptop.** Nothing sent in Codex on the laptop.
- Bingo, Codex: five-hour 77%, weekly 14%, reading 49 minutes old. Unchanged, as expected.
- Codex app: five-hour 18% left (82% used), weekly 85% left (15% used). Matches the desktop exactly.
- The Codex app refreshed from the server without a turn (decision criteria, fourth row). Bingo on the laptop is 5 points behind the account on five-hour and 1 on weekly.

**Step 4, one turn, laptop, reported 22:20.** One short Codex turn sent on the laptop.
- Bingo, Codex: five-hour 82%, weekly 15%. The new record includes the desktop's usage; the short turn added less than a point.
- Codex app: five-hour 18% left (82% used), weekly 85% left (15% used). Matches Bingo exactly.
- Outcome: decision criteria, first row. The laptop's log caught up to the account figure on its next turn.

**Step 5, browser or cloud as "elsewhere":** not run yet. Trevor does not use Codex in the browser. He uses it from his phone, rarely. Phone use likely runs as a cloud task rather than on a machine with logs, so it is the one "elsewhere" in his use that the desktop run does not cover. Worth one run with the phone as step 2.

**Habit question (decision criteria, second row), answered 2026-11-03.** Yes. Trevor moves between several computers and devices and wants to check how much Codex usage is left before starting a project, on whichever machine he is at, without sending a turn first. A figure that is only right after a turn on this machine misses that use.

## Result

(open)
