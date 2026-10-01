# HUD Display Options — Technical Plan

## Technical Approach

Both settings ride the path 0.1.0 already built, and neither adds a component. Core decides
every word and number; the shell places them. That split is kept exactly: the countdown phrase and
the bar's per-segment fill are both computed in Core, where they can be tested, and the shell
only draws what it is handed. The WPF layer has no tests, so anything that can be wrong in a way
a test could catch must not live there.

Data flow, with the changes marked:

```
UserSettings (+ResetFormat, +ShowBar)          ← persisted by SettingsStore, toggled by TrayIcon
        │
Readout.Lines(state, settings, now, culture)
        ├─ Reset(...)  → ResetFormatter.Describe (clock time, unchanged)
        │              → ResetFormatter.Countdown (new)          when ResetFormat = Countdown
        └─ ReadoutLine (+Bar: IReadOnlyList<double>?)  ← Bar.Segments(UsedPercent) when ShowBar
        │
HudContent.Reading → HudWindow.Render  (+ one grid column for the bar, drawn from Bar)
```

The status mark, collapse, severity, and the empty-state words are untouched. Because the bar is
attached to a `ReadoutLine`, it exists only where a line exists: no reading means no line means
no bar (AC-12) without a rule to say so. Because the bar's colour is read from the line's
`Severity`, which is already `Normal` for a frozen reading, the bar and its figure cannot differ
in colour (AC-11) without a rule to say so either.

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Where the countdown lives | A new `ResetFormatter.Countdown`, beside `Describe` | Same inputs, same null rule for a missing reset (AC-4), same tests file. `Readout.Reset` picks one by setting; the "limited" prefix (quota-hud AC-6) applies to either unchanged. |
| Countdown under a minute, and past the reset | `<1m` for both | Rounding down gives "0m", which reads as already reset, and announcing a reset is a claim about a reading not yet taken (the reason 0.1.0 says "resets any moment"). `<1m` stays true in both cases and is short. Signed off by Trevor 2026-10-01. |
| Unit boundaries | Days at ≥ 24h, hours at ≥ 1h, minutes below | Per AC-2. 23h 59m reads "23.9h", 24h reads "1.0d". Each boundary gets a test on both sides. |
| Truncating to a tenth | `Math.Floor(value * 10) / 10`, formatted `"0.0"` with the caller's culture | Rounding down per AC-2; the format string forces the trailing ".0"; the culture gives the locale's decimal mark. |
| How the bar is described | `ReadoutLine.Bar` is ten fill fractions (each 0.0 to 1.0), null when the setting is off | The arithmetic that can be wrong (partial segment, over-100 clamp, negative input) is a pure function in Core with tests. The shell draws ten boxes and sizes each one's fill; it computes nothing. |
| Bar fill above 100 or below 0 | Clamped to 0..100 before splitting into segments | AC-13 for the top. A negative figure is not expected but would otherwise produce negative widths; clamping the drawing does not change the figure beside it, which still shows what the server sent. |
| Bar colour when normal | The figure's own white, dimmed; track a dark grey | Normal has no severity brush today (the figure uses the style's white). The bar follows the same rule: one decision about what an unremarkable figure looks like. Warning, critical, and rate-limited reuse the three existing brushes. |
| Where the toggles go | Two new tray menu items beside "Collapse" and "Show percentage remaining" | AC-14: where the existing display settings are. Both are on/off, so `CheckOnClick` items fit, and the menu already reads its checkmarks from settings on open. |
| Shape of the setting | `ResetFormat` enum (`ClockTime`, `Countdown`); `ShowBar` bool | Reset format is a choice between two named behaviours, so an enum names them in the settings file. The bar is on or off, which is what a bool is. |
| Reading an older settings file | New fields optional in `FileShape`, defaulting to 0.1.0's behaviour | A 0.1.0 `settings.json` has neither field. Same pattern the store already uses for every field, and the route to AC-16. |
| On-screen verification | Restore `scripts/stub-usage-server.js` from git history as a spike for this feature, then delete it | It was built for exactly this (forcing chosen percentages through the Debug overrides) and closed at 7.3a. Bringing it back costs nothing; the spike document records it. |

## Data Model

```csharp
// Settings — two fields added, defaults keep 0.1.0's HUD.
public enum ResetFormat { ClockTime, Countdown }

public sealed record UserSettings(
    HudPosition? Position,
    bool Collapse,
    DisplayDirection Direction,
    Thresholds Thresholds,
    ResetFormat ResetFormat,     // default ClockTime
    bool ShowBar);               // default false

// Display — one field added. Null: no bar. Otherwise exactly ten values, each 0.0..1.0.
public sealed record ReadoutLine(
    string Window, string Percent, string? Reset, Severity Severity,
    IReadOnlyList<double>? Bar);
```

`HudContent.SameAs` compares lines with `SequenceEqual`, which uses the record's own equality, and
a record compares an `IReadOnlyList` member by reference. Two identical bars would compare unequal
and the HUD would rebuild every second. `ReadoutLine` therefore needs its equality written out to
compare `Bar` by value, pinned by a test. This is the same trap `SameAs` itself documents.

## API Contracts

Internal seams only.

- `ResetFormatter.Countdown(DateTimeOffset? resetsAt, DateTimeOffset now, CultureInfo? culture) → string?`
  — `null` when `resetsAt` is null; otherwise `"4.5d"`, `"2.3h"`, `"53m"`, or `"<1m"`.
- `Bar.Segments(double usedPercent) → IReadOnlyList<double>` — ten values; segment *i* holds
  `clamp(used/10 − i, 0, 1)` after `used` is clamped to 0..100.
- `Readout.Lines` — signature unchanged; reads the two new settings.
- Settings file — two optional keys, `resetFormat` (`"ClockTime"` | `"Countdown"`) and
  `showBar` (bool).

## Implementation Phases

1. **Settings** — the two fields, their defaults, load and save round-trips, a 0.1.0 file loading
   to defaults.
2. **Core display logic** — `ResetFormatter.Countdown`; `Bar.Segments`; `ReadoutLine.Bar` with
   value equality; `Readout` choosing by setting. Countdown and bar are independent of each other
   and can be built side by side.
3. **Shell** — tray toggles; the bar column in `HudWindow`.
4. **On-screen verification** — the four combinations of the two settings, across normal, warning,
   critical, rate-limited, stale, and frozen, captured from the built exe with the stub.
5. **Release prep** — `CHANGELOG.md` `[Unreleased]` entries, version to `0.2.0`, README if it
   describes the HUD's lines.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Bar equality by reference makes the HUD rebuild every second | Wasted layout passes, possible flicker; invisible in tests unless asked | Explicit value equality on `ReadoutLine`, with a test that two lines built separately compare equal. |
| The HUD gets wider with the bar on and crosses a monitor seam or edge | HUD partly off screen | `StayOnScreen` already runs after a size change; checked on screen in phase 4, at the right edge where the HUD sits by default. |
| Decimal tenths of an hour misread as minutes ("2.5h" read as 2h 50m) | A user misjudges how long they have | Accepted in the spec. Exact times stay in the panel. Revisit if it bites in use. |
| Tenths in a comma-decimal locale ("4,5d") sit awkwardly beside the figure's own punctuation | Cosmetic | Tested with a comma culture so the output is at least deliberate. |
| The countdown phrase is shorter than the clock phrase, so switching settings changes the HUD's width | The HUD shifts at its anchored edge | The same resize path every reading change already uses. Checked on screen. |
| A 0.1.0 settings file loads wrong and resets the user's position or thresholds | Annoying regression on upgrade | A test loads a literal 0.1.0 file and expects every old field kept and both new ones defaulted. |
