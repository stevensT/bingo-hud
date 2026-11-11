# Scoped Cap Shape — Technical Plan

## Technical Approach

The change is confined to how `UsageNormalizer.ReadLimitsArray` reads `scope`. Everything
downstream already handles a per-model cap: `QuotaWindow.Scope` is the label string the panel
shows, `PanelReadout` lists `WeeklyScoped` windows by it, and the HUD and alerts already leave
them out. So `QuotaWindow` keeps its `string? Scope`. What changes is where that string comes from:
`scope.model.display_name`, then `scope.model.id`, instead of `scope` itself.

`ReadScope` becomes a three-way reading instead of a nullable string:

| Input | Reading |
|---|---|
| `scope` absent or `null` | no scope; classify by `kind` as today |
| object, `surface` absent or `null`, `model` an object with a non-empty `display_name` or `id` string | scope with that label; classified `WeeklyScoped` |
| anything else | malformed, with a reason; the response is unreadable |

The existing inline check at `UsageNormalizer.cs:194-199` (scope must be a string or null) is
folded into this reading, so there is one place that decides what a scope is.

The capture script fix is a lookup order: `creds.claudeAiOauth` first, the existing recursive
search only when that object is absent. That keeps the script working for a credential file
from an older layout.

## Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Unseen scope shape | Unreadable | Same fail-closed rule as before. A skipped entry would vanish silently, and a misread one could be alerted on as the account-wide week. |
| String-form scope | Unreadable | Never observed. Keeping it is code and tests for a hypothetical. |
| Label | `display_name`, then `id`, unaltered | Both are the server's own words. `id` was `null` in the capture, so `display_name` is the one that carries a value today. |
| Unknown keys inside `scope` | Ignored | Consistent with the top level. Pinning them is the contract test's job, not the parser's. |
| `QuotaWindow.Scope` type | Stays `string?` | It already means "the label the panel shows". Widening it to a structured type would touch every display test for no behaviour change. |
| Reason wording | One reason per failure kind | The reason goes on screen verbatim for bug reports, so it should say what was wrong. |
| New fixture name | `2026-11-11-scoped-cap` | Names what the capture is for, like `baseline` and `auth-failure`. |
| Baseline contract test | Untouched | It pins the 2026-08-30 capture, which has not changed. The new capture gets its own, narrower contract test on `limits`. |

## Data Model

No type changes. `QuotaWindow(Kind, UsedPercent, ResetsAt, Severity, Scope)` where `Scope` is now
documented as the server's label for the model the cap is restricted to.

Observed scope, 2026-11-11:

```json
"scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null }
```

## Interface

Unreadable reasons added (each is a whole sentence, shown verbatim):

- `An entry of the limits array had a scope that was not an object.`
- `An entry of the limits array had a scope that named no model.`
- `An entry of the limits array had a scope restricted to a surface, which this version does not read.`

The old reason `…had a scope that was not a string.` is removed.

## Implementation Phases

1. **Parsing** — scope reading, unreadable cases, fixture and contract test.
2. **Script and docs** — capture token lookup, comments, plan row, changelog.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| The server changes the scope shape again | Readout goes unreadable again | Intended. The contract test on the new fixture makes the change visible in the suite as well as on screen. |
| A `surface`-scoped cap appears soon | Unreadable until a fix ships | Accepted by decision. A capture of it would be the input to the next fix. |
| `weekly_scoped` reset times carry no fractional seconds, unlike the others | A strict regex on reset times would fail | `DateTimeOffset.TryParse` already accepts both. The new contract test does not apply the baseline's six-digit regex to the scoped entry. |
