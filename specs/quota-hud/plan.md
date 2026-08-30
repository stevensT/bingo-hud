# Quota HUD — Technical Plan

## Technical Approach

Two projects with one hard rule between them: `TokenWatch.Core` decides, `TokenWatch.App` draws.
Every behaviour in the spec that can be stated as "given X, show Y" belongs in Core and is tested
there. The WPF layer holds no logic, only rendering and Win32 interop.

The data path is a single line with no branching ownership:

```
ICredentialProvider → IUsageClient → UsageNormalizer → QuotaSnapshot → SeverityPolicy
                                                              ↓
                                              AlertEngine + AlertStateStore
                                                              ↓
                                              QuotaMonitor (observable state)
                                                              ↓
                                                    App: HUD / panel / toast
```

`QuotaMonitor` is the only stateful orchestrator. It owns the current `ReadingState`, holds the
single-flight guard, and exposes one observable snapshot the shell binds to. Everything upstream of
it is either a pure function or a thin I/O adapter behind an interface — which is what makes the
interesting behaviour testable without a network, a clock, or a window.

Two deliberate purity choices carry most of the testing weight:

**The poll policy reads nothing.** `PollPolicy.NextDelay(signals)` returns a delay and a named
reason, as a pure function over a `PollSignals` record gathered by the caller immediately before
each tick. It touches no clock, no power state, no network. The whole cadence table is then a
data-driven test with no mocking, and the named reason is surfaced in the detail panel so cadence
is inspectable rather than mysterious.

**Time is injected.** `IClock` everywhere in Core. Staleness, reset countdowns, window-occurrence
identity, and backoff are all deterministic under test.

## Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Stack | C# + WPF, .NET 8 | Frameless transparency, topmost, click-through, tray, toasts, and DPAPI are native rather than fought for. Single self-contained exe, ~30–50 MB idle — the footprint argument dominates for an always-running widget. Owner is new to Windows development; this is the best-documented option. |
| Project split | `Core` (no UI reference) + `App` (WPF) | UI code is awkward to test; the logic most likely to harbour bugs must not live in it. The compiler enforces the seam — Core cannot reference WPF. |
| Quota source | `GET /api/oauth/usage` only | Sole source that knows the account's real ceiling. No plan limits are hardcoded anywhere in this codebase. |
| Parsing posture | Tolerant in, strict out | Two independent prior-art parsers disagree about this payload's shape, and our own inspection found flat `seven_day_opus` keys where the research describes a `weekly_scoped` container. Unknown fields are ignored; a window we cannot identify is an explicit failure, never a silent zero. |
| Raw response retained | Yes, last raw body kept in memory | When the payload drifts, the first diagnostic question is what it actually sent. Not persisted to disk — it is an authenticated response. |
| Credentials | Read `~/.claude/.credentials.json` | No third-party OAuth client registration exists for consumer plans. All three prior-art tools do exactly this. |
| Token refresh | Shell out to `claude -p .`, then re-read | No OAuth refresh grant is implemented locally — none of the prior art does either, and inventing one is a credential bug waiting to happen. |
| Credential re-read | Watch signature over path, size, mtime | Avoids re-reading and re-authenticating on a timer. Cheapest-first. |
| Poll cadence | Pure-function policy, 2–30 min | Adopted from CodexBar. Never faster than 2 minutes: this is someone else's undocumented service, and utilization only moves when Claude Code is working. |
| Percentage direction | Display remaining | The endpoint returns utilization; all three prior-art tools invert before display. The number a person acts on is what is left. |
| Severity | Three discrete states | Warning at 25% remaining, critical at 10%. A gradient reads as noise at HUD size. |
| Alert identity | Window kind + threshold + `resets_at` | `resets_at` is the natural identity of a window occurrence, so rearming on reset is free and restart-safe. |
| Alert state persistence | Local file, plain JSON | Must survive restart mid-window (AC-16). Holds no secrets. |
| Interop isolation | All P/Invoke in one file | Topmost, click-through, edge-snap, and DPAPI probing are the only unsafe-ish code; quarantining them keeps the rest ordinary C#. |
| Version display | `<Version>` in csproj, shown in detail panel | When the payload drifts, "which build is misreading it" must be answerable from the screen. |

### Constitutional tension worth naming

Principle 4 favours integration tests over unit tests. The highest-value tests here are
fixture-driven tests of `UsageNormalizer` and table-driven tests of `PollPolicy` — unit tests by
any definition. That is the correct trade for this feature: the integration seam we cannot
exercise is the live endpoint, and the failure mode we most need to catch is a payload shape we
did not anticipate. Recorded fixtures are the only honest way to test that. Integration tests
still cover the assembled `QuotaMonitor` against a stub client, exercising real state transitions
across polls.

## Data Model

```csharp
enum WindowKind { FiveHour, SevenDay, WeeklyScoped }

record QuotaWindow(
    WindowKind Kind,
    string Label,            // display name, post-normalization
    double RemainingPercent, // 0-100, already inverted from utilization
    DateTimeOffset ResetsAt,
    string? ModelScope);     // "opus" / "sonnet" for WeeklyScoped, else null

enum ServerStatus { Allowed, AllowedWarning, Rejected }

record QuotaSnapshot(
    IReadOnlyList<QuotaWindow> Windows,
    ServerStatus Status,
    DateTimeOffset ObservedAt,
    string RawBody);

enum Severity { Normal, Warning, Critical, RateLimited }
enum Freshness { Fresh, Stale, Frozen }
enum AuthFailureKind { Invalidated, SignedOut, PermissionDenied }

// The single discriminated outcome of one fetch attempt.
abstract record FetchOutcome
{
    record Success(QuotaSnapshot Snapshot) : FetchOutcome;
    record AuthFailed(AuthFailureKind Kind) : FetchOutcome;
    record Transient(TimeSpan? RetryAfter) : FetchOutcome;   // 429, 5xx, network
    record Unsupported(int StatusCode) : FetchOutcome;       // unusable on this account
    record Unreadable(string Reason) : FetchOutcome;         // parsed nothing we recognize
}

// What the shell binds to. Exactly one of these is current at any time.
record ReadingState(
    QuotaSnapshot? Last,       // null until a first success
    Freshness Freshness,
    FetchOutcome? LastFailure,
    TimeSpan Age,
    string PollReason);

record PollSignals(
    bool PowerConstrained,
    TimeSpan? SinceUserOpenedPanel,
    TimeSpan? SinceLocalTranscriptActivity,
    bool LastAttemptFailed,
    TimeSpan? ServerRetryAfter);

record AlertKey(WindowKind Kind, int ThresholdPercent, DateTimeOffset ResetsAt);
```

`Freshness.Frozen` is the AC-13 case: a reading that can no longer be refreshed. It stays on
screen, marked, and is excluded from the worst-window severity calculation so a dead reading
cannot own the headline.

## API Contracts

### Upstream (undocumented — treat as unstable)

```http
GET https://api.anthropic.com/api/oauth/usage
Authorization:   Bearer <oauth access token>
anthropic-beta:  oauth-2025-04-20
Accept:          application/json
User-Agent:      claude-code/<version>
```

The `User-Agent` is pinned deliberately: prior art reports that generic agents land in a stricter
429 bucket on this endpoint.

**Response fields consumed.** Utilization arrives as 0–100 and is inverted on the way in.

| Field | Consumed as | Aliases seen in the wild |
|---|---|---|
| `five_hour` | Session window | `5_hour`, `session`, `primary` |
| `seven_day` | Weekly window | `7_day`, `weekly`, `week`, `secondary` |
| `seven_day_opus`, `seven_day_sonnet`, `weekly_scoped` | Per-model weekly caps | flat keys, array, or keyed object |
| `.utilization` | `100 - value` becomes `RemainingPercent` | — |
| `.resets_at` | `ResetsAt` | ISO-8601 |
| `status` | `ServerStatus` | `allowed`, `allowed_warning`, `rejected` |
| `spend` | Ignored | Deliberately not consumed — non-goal |

Normalization rules: alias-map window keys on the way in; accept every container form for
per-model caps; ignore unknown fields; treat an unrecognizable window shape as `Unreadable`,
never as zero.

**Error taxonomy** (adopted from prior art):

| Class | Statuses | Action |
|---|---|---|
| Auth | 401, 403 | Attempt one CLI refresh, then surface sign-in. A 401 whose body carries `error.type == "authentication_error"` is a genuine invalidation; a 401 with no parseable body stays generic. |
| Transient | 429, 5xx, network | Back off. Honour `Retry-After`. |
| Unsupported | any other status | Endpoint not usable on this account — surface plainly and stop polling hard. |

A 429 must never trigger a compensating request against any other endpoint (AC-26). The fallback
that would do so is a non-goal, and the reason is worth stating: it would spend real quota to read
a number, and add load to the very limit that caused the failure.

### Internal seams

```csharp
interface IClock               { DateTimeOffset Now { get; } }
interface ICredentialProvider  { Task<Credential?> GetAsync(CancellationToken ct); }
interface ICredentialRefresher { Task<bool> TryRefreshAsync(CancellationToken ct); }
interface IUsageClient         { Task<FetchOutcome> FetchAsync(Credential c, CancellationToken ct); }
interface IAlertStateStore     { bool HasFired(AlertKey k); void MarkFired(AlertKey k); void Prune(DateTimeOffset before); }
interface INotifier            { void Raise(Severity s, string title, string body); }

static class PollPolicy        { static (TimeSpan Delay, string Reason) NextDelay(PollSignals s); }
static class SeverityPolicy    { static Severity Evaluate(QuotaSnapshot s, Thresholds t); }
```

`ICredentialProvider` is the seam the credential decision sits behind. `FileCredentialProvider` is
the only implementation planned; the interface exists because it is also the natural test double,
not as speculative abstraction.

## Implementation Phases

1. **Foundation** — solution, both projects, test project, `IClock`, and recorded fixtures
   captured from real responses with credentials scrubbed.
2. **Parsing** — `UsageNormalizer` against those fixtures, including the alias and container-form
   variants, plus a contract test pinning the known-good shape. Highest risk and highest test
   value, so it goes first and it goes alone.
3. **Credentials** — `FileCredentialProvider`, watch signature, the seconds-versus-milliseconds
   `expiresAt` normalization, the two-probe permission-denied distinction, and the CLI refresh
   shellout.
4. **Polling and state** — the `PollPolicy` table, `QuotaMonitor` with single-flight and backoff,
   and the full `ReadingState` transition set including Frozen.
5. **Alerts** — threshold crossing, dedupe by `AlertKey`, persistence, rearm on reset, mute.
6. **Shell** — HUD window, the interop file, edge snapping, click-through, detail panel, tray,
   toasts, settings persistence.
7. **Polish** — error copy, version display, changelog entry for `0.1.0`.

Phases 2 through 5 are all Core and all testable headless. Phase 6 is where the Windows-specific
difficulty concentrates, and it is deliberately last so that everything it renders is already
known-correct.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Upstream payload changes shape | High — the app's only data source | Tolerant parsing, alias maps, contract fixture test, explicit `Unreadable` state showing no numbers, raw body retained for diagnosis, MINOR version bump discipline so a break is traceable to a build |
| Endpoint removed or gated entirely | High — feature ceases to function | `Unsupported` outcome surfaces plainly and stops hard polling. No silent fallback; the header-based fallback stays a non-goal. |
| Aggressive polling draws rate limiting | Medium — could affect the user's actual Claude Code work | 2-minute floor, single-flight guard, pinned User-Agent, `Retry-After` honoured, manual refresh subject to the same backoff |
| Refresh shellout flashes a console window | Low, but visible and unnerving | `CREATE_NO_WINDOW` (0x08000000), stdio nulled, `CLAUDECODE` and `CLAUDE_CODE_ENTRYPOINT` stripped from the child environment |
| Refresh shellout hangs | Medium — poller stalls | Hard timeout, treated as `Transient`, never awaited on the UI thread |
| Permission-denied misread as signed-out | Medium — sends the user down the wrong recovery path | Two-probe check: a metadata-only probe needing no elevated access distinguishes "exists but refused" from "absent" (AC-11) |
| Click-through makes the HUD unclickable | Medium — user cannot open the panel | Hit-testing toggles on hover rather than being permanently off; explicit test of the enter and leave transitions before shipping |
| WPF interop difficulty on a first Windows project | Medium — schedule, not correctness | All P/Invoke quarantined in one file; Core carries the logic and is ordinary C# |
| Credential token leaks into logs or error text | High if it happened | Token never logged, never included in error text, never written anywhere. Raw response body kept in memory only. |
| Fixtures drift from reality unnoticed | Medium — tests pass while the app breaks | Fixtures are dated and their capture date recorded; a failing contract test is the signal to recapture, never to loosen the parser |
