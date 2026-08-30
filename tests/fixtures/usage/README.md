# Usage endpoint fixtures

Recorded responses from `GET https://api.anthropic.com/api/oauth/usage`, captured with
`scripts/capture-usage.js`. These are the only honest way to test a parser against an
undocumented payload, so they are dated and their capture conditions recorded.

Each capture writes two files:

- `<date>-<label>.json` — the response body, scrubbed.
- `<date>-<label>.meta.json` — status, headers, byte count, and the `User-Agent` sent.

**Scrubbing.** Emails, UUIDs, and opaque identifiers are replaced in both body and headers.
The bodies observed so far carry no account data at all; the identifiers live in the response
headers (`anthropic-organization-id`, `anthropic-workspace-id`, `request-id`, `cf-ray`), which
are redacted by key. Raw unscrubbed bodies are written only outside the repository, and only
when `--raw-dir` is passed.

**When a contract test fails, recapture — never loosen the parser.** A failing contract test is
the signal that the payload moved, which is the single most important thing this project needs
to detect.

## Inventory

| Fixture | Status | Conditions |
|---|---|---|
| `2026-08-30-baseline` | 200 | Max plan, both windows normal. `five_hour` 12% utilized, `seven_day` 37%. Per-model weekly keys present but `null`. |
| `2026-08-30-auth-failure` | 401 | Deliberately invalid bearer token (`--auth-failure-probe`). Body carries `error.type == "authentication_error"`. |
| `2026-08-30-derived-flat-only` | 200, derived | **Not a capture.** The baseline body with its `limits` array removed. |

### Derived fixtures

One fixture is not a recorded response. The endpoint currently returns `limits[]` on every
request, so the flat-key fallback — the path an older account may still take — cannot be captured
from this account at all. `2026-08-30-derived-flat-only` is the baseline body with one key
removed and nothing else changed, which is the only edit that keeps it honest: every value in it
is still a value the server actually sent.

Derived fixtures are named `derived` and carry no `.meta.json`, because there was no response
to record the status and headers of. Regenerate one by deriving it from its capture again, never
by editing values in place.

## Still missing

These cannot be manufactured and must be captured opportunistically, when the account happens
to be in the state:

- A window at warning severity (25% remaining) and at critical (10%).
- A `429`, and whatever `severity` a rejected window reports.
- A response where `seven_day_opus` / `seven_day_sonnet` carry data rather than `null`.
- Any non-401 unsupported status, to confirm the `Unsupported` branch.

## Recapturing

```
node scripts/capture-usage.js --label <name>
node scripts/capture-usage.js --label auth-failure --auth-failure-probe
```

The access token is read from `~/.claude/.credentials.json` into memory, used for the one
request, and never printed or written to disk.
