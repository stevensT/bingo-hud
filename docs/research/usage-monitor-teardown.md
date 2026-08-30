# Usage monitor teardown — prior art for Bingo

Source review of three shipping Claude-usage monitors, read at the source rather than from
their READMEs. Compiled 2026-08-29.

| Project | Stack | Surface | Scope |
| --- | --- | --- | --- |
| [CodeZeno/Claude-Code-Usage-Monitor](https://github.com/CodeZeno/Claude-Code-Usage-Monitor) | Rust | Windows taskbar widget | Claude + 4 optional providers |
| [steipete/CodexBar](https://github.com/steipete/CodexBar) | Swift 6.2, macOS 14+ | Menu bar + CLI | 69+ providers |
| [timharris707/modeldeck](https://github.com/timharris707/modeldeck) | SwiftUI + Node 24 + SQLite | Menu bar popover | Claude + Codex, multi-account |

> **All endpoint and header details below are reverse-engineered from shipping clients, not
> from published Anthropic documentation. Treat the payload shape as unstable.**

---

## 1. The usage endpoint

All three projects independently converged on the same undocumented endpoint. This is the
single highest-value finding.

```http
GET https://api.anthropic.com/api/oauth/usage

Authorization: Bearer <oauth access token>
anthropic-beta: oauth-2025-04-20
Accept:         application/json
User-Agent:     claude-code/2.1.83
```

**Pin the `User-Agent`.** ModelDeck sets it deliberately, with the comment: *"Generic user
agents land in a stricter 429 bucket on this endpoint."*

### Response envelope

| Field | Carries | Notes |
| --- | --- | --- |
| `five_hour` | Session window | `utilization` (0–100) + `resets_at` (ISO-8601) |
| `seven_day` | Weekly window | Same shape |
| `weekly_scoped` | Per-model weekly caps | Array *or* keyed object; where Opus-class limits surface |
| `spend` | Paid overflow credits | `enabled`, `used`, `limit` as minor units + `exponent` |
| `rate_limits` | Newer array form | ModelDeck accepts this *or* the keyed form above |

### Schema drift is real

The Rust client models a fixed shape; ModelDeck's parser accepts a much looser one — strong
evidence the payload has moved more than once. ModelDeck normalizes:

- `five_hour` | `5_hour` | `session` | `primary` → one window label
- `seven_day` | `7_day` | `weekly` | `week` | `secondary` → another

Money objects each carry their **own** `exponent`. ModelDeck rescales both to the larger
exponent rather than assuming cents; a mismatch there renders a 10× wrong dollar figure.
It rejects exponents outside an integer 0..6 range rather than guessing.

### Error taxonomy (worth copying verbatim)

The Rust client sorts failures into three buckets:

| Class | Statuses | Action |
| --- | --- | --- |
| `Auth` | 401, 403 | Re-login required |
| `Transient` | 429, 5xx, network | Back off and retry later |
| `Unsupported` | any other status | Endpoint not usable on this account — use fallback |

**429 must never fall through to the Messages-API fallback.** The in-source rationale: doing
so would spend real quota on a request whose only purpose is reading headers, and would add
load to the very rate limit that caused the failure.

ModelDeck additionally classifies a 401 on the structured body `error.type ===
"authentication_error"` as a genuine server-side token invalidation (revoked or rotated),
distinct from a transient 401. A 401 with a missing or unparseable body stays generic.

---

## 2. Fallback: rate-limit headers on the Messages API

When the usage endpoint is unavailable for an account, the Rust client burns a one-token
request and reads headers off the response.

```http
POST https://api.anthropic.com/v1/messages
anthropic-version: 2023-06-01
anthropic-beta: oauth-2025-04-20

{"model":"claude-3-haiku-20240307","max_tokens":1,
 "messages":[{"role":"user","content":"."}]}
```

Response headers:

```
anthropic-ratelimit-unified-5h-utilization           0.42        # 0–1 FRACTION, not percent
anthropic-ratelimit-unified-5h-reset                 1756500000  # unix seconds
anthropic-ratelimit-unified-7d-utilization           0.71
anthropic-ratelimit-unified-7d-reset                 1756900000
anthropic-ratelimit-unified-reset                    1756500000  # overall
anthropic-ratelimit-unified-status                   allowed | rejected
anthropic-ratelimit-unified-representative-claim     five_hour | seven_day
```

Two gotchas:

1. Utilization here is a **0–1 fraction**, whereas `/api/oauth/usage` returns **0–100**.
2. When `status: rejected` and both utilizations read zero, `representative-claim` names the
   window that is actually exhausted — pin that one to 100%.

Model fallback chain used for the probe request:
`claude-3-haiku-20240307` → `claude-haiku-4-5-20251001`.

The Rust client also *merges*: if the usage endpoint returns utilization but omits
`resets_at`, it fills the reset timers in from the Messages API rather than discarding either.

---

## 3. Credentials

None of the three prompt for an API key. All read a token that already exists on the machine.

| Platform | Location | Read via |
| --- | --- | --- |
| Windows | `~/.claude/.credentials.json` | Plain file read |
| Windows (desktop app) | `%APPDATA%\Claude\claude-code\<version>\` | Encrypted token cache; app refreshes itself |
| WSL | `wsl.exe -d <distro> -- cat ~/.claude/.credentials.json` | Lazy — only if no local token |
| macOS | `security find-generic-password -s <service> -a <user> -w` | Keychain, after the file read fails |

Credential JSON shape:

```jsonc
{ "claudeAiOauth": { "accessToken": "...", "expiresAt": 1756500000 } }
```

ModelDeck also accepts an `oauth` key or a bare root object, and normalizes seconds vs.
milliseconds by testing `expiresAt < 10_000_000_000`.

### Token refresh

**None of them implement the OAuth refresh grant.** They shell out to the CLI and let it
rewrite the credentials file, then re-read it:

```
claude -p .
```

…with `CLAUDECODE` and `CLAUDE_CODE_ENTRYPOINT` removed from the environment, stdio nulled,
and (on Windows) the `CREATE_NO_WINDOW` (`0x08000000`) creation flag so no console flashes.

The Windows binary is resolved as `claude.cmd`, falling back to the desktop app's bundled
build under `%APPDATA%\Claude\claude-code\<version>\claude.exe` — sorted by parsed version
number, since directory order is not version order.

### Cheap change detection

Rather than re-authenticating on a timer, the Rust client builds a watch signature per source
and only re-reads when it changes:

- Local file: `win:<path>|present|<size>|<mtime>` or `win:<path>|missing`
- WSL: `stat -c 'present|%s|%Y'`, or `missing`

The WSL probe sits behind a lazy iterator, so a machine that resolves a token locally never
spawns `wsl.exe` at all. Credential sources are tried cheapest-first: local file → desktop app
cache → WSL distros.

### Distinguish "permission denied" from "signed out"

ModelDeck issue #98: a dismissed macOS Keychain prompt is indistinguishable from a missing
credential unless you probe twice. A metadata lookup (same command *without* `-w`) needs no
ACL approval and never prompts — if that succeeds while the secret read failed, the item
**exists** and access was refused. Surfacing that as "sign in again" sends users down the
wrong recovery path.

Worth designing the Windows equivalent (DPAPI / cache-locked) before shipping an error state.

Also: never surface raw `security` output to the user — it can contain the credential value.

---

## 4. Percentages and tokens are DIFFERENT data sources

This distinction drives scope more than anything else in this document.

| Want | Source | Gives you | Does not give you |
| --- | --- | --- | --- |
| "Can I keep working?" | `/api/oauth/usage` | Utilization %, reset times, per-model caps, spend | Token counts |
| "What have I spent?" | Local JSONL transcripts | Tokens, cost, per-project/session attribution | Remaining quota |

The usage endpoint is the only source that knows the account's actual plan ceiling — which is
why none of these tools hardcode plan limits.

### Transcript ingestion (the token side)

Walk `<profile>/projects/**/*.jsonl`. Take `message.usage` off records and sum **all four**
counters:

```
usage.input_tokens
usage.cache_creation_input_tokens    # may also be nested under usage.cache_creation
usage.cache_read_input_tokens
usage.output_tokens
```

**Deduplicate or you will double-count** — the same API call appears in more than one record.
ModelDeck keys on the first available of:

1. `request:<requestId>`
2. `message:<sessionId>:<messageId>`
3. `record:<sessionId>:<recordUuid>`

The fallback chain exists because older records carry `requestId` and no effort field, while
current records carry effort and no `requestId`.

Two edge cases it handles explicitly:

- Records are **not always** `type === "assistant"`. A small legacy population of refusals
  carries `requestId` and usage on a non-assistant record; those are real API calls that count.
- `isSidechain` (falling back to whether the file is a subagent transcript) separates subagent
  turns so they do not silently inflate the parent session.

---

## 5. Polling cadence

CodexBar's adaptive policy is a **pure function** of five inputs returning a delay and a named
reason. First match wins; every result lands in 2–30 minutes by construction.

| Condition | Delay | Reason |
| --- | ---: | --- |
| Low Power Mode, or thermal state serious/critical | 30 min | `constrained` |
| Menu opened ≤ 5 min ago | 2 min | `recentInteraction` |
| Menu opened 5 min – 1 h ago | 5 min | `warm` |
| Local transcript activity < 5 min ago | 5 min | `codingActivity` |
| Menu opened 1–4 h ago | 15 min | `idle` |
| Never opened, or 4+ h ago | 30 min | `longIdle` |

Design notes:

- The policy reads **no clock and no system state itself** — the caller gathers those
  impure signals immediately before each tick and passes them in. This makes the whole table
  testable without mocking time.
- It deliberately excludes quota level, latency, error state, account, and time of day. The
  decision record rejects per-account prediction and learned ranking as "harder to audit."
- Fixed alternatives offered: Manual, 1m, 2m, 5m, 15m, 30m.
- Only one provider-batch refresh runs at a time regardless of cadence mode (coalescing guard).

---

## 6. UI conventions the three converged on

Independently, on different platforms, all three arrived at nearly the same readout. Treat
these as defaults rather than open decisions.

- **Display "% left", not "% used."** The API returns utilization; every one of these tools
  inverts it before display. The number a person acts on is what remains.
- **Reset time is co-equal with the percentage** — same line, not a tooltip, in the user's own
  timezone. Switches from absolute ("Resets Sat 4:38 AM") to relative ("Resets in 53 min") as
  it approaches.
- **Three discrete states, not a gradient.** ModelDeck: plain / gold / red at 25% and 10%
  remaining. The tray icon is driven by the single worst window across all accounts.
- **Spend never drives the headline severity.** Deliberate call in ModelDeck (issue #28): paid
  overflow is the least important signal for a subscription user, so it counts only when no
  rate-limit scope exists at all. The Rust client goes further — it hides the credits gauge
  entirely until a plan window actually hits 100%.
- **Collapse to the worst window.** One interesting window → one row; multiple windows expand
  in place. Sort order is user-switchable (by reset time, by percent, or grid).
- **Always show data age.** "Oldest data 1 min ago" sits permanently in the footer. ModelDeck
  marks a row stale past 15 min, and *freezes* percentages for an account that can no longer
  refresh so a dead account cannot own the headline number.
- **Burn rate is optional.** Only CodexBar does it ("On pace", "Lasts until reset", "% in
  reserve"). Two of three shipped without it.

---

## 7. Implications for Bingo

- **Build the endpoint path first.** Percentages, reset times, per-model caps and spend all
  arrive in one authenticated GET; the display is downstream of it.
- **Decide early whether tokens are in scope.** Transcript ingestion is a substantially larger
  project than quota polling — file walker, dedupe scheme, and a store. If the question is "can
  I keep working," the usage endpoint alone answers it.
- **The Windows surface is proven.** CodeZeno is a Rust taskbar widget reading
  `~/.claude/.credentials.json` with WSL and desktop-app fallbacks, including the CLI-shellout
  refresh — on the exact target platform.
- **Parse defensively and keep the raw response.** Two independent parsers written against the
  same endpoint disagree about its shape. Normalize window names on the way in.
