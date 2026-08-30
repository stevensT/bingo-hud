# Bingo

A small always-on-top readout for Windows showing how much of your Claude subscription usage
window is left, and when it resets.

Claude Code on a Max or Pro plan is governed by rolling usage windows rather than per-token cost.
The thing that actually interrupts you is running out of window mid-task, and the only way to
check is to stop and run `/usage` in a session — which is the interruption you were trying to
avoid. Bingo keeps the answer in peripheral vision so it arrives before it matters.

**Status:** pre-implementation. The design is settled and specified; no source yet.

## What it shows

A frameless, draggable, always-on-top window with the remaining percentage and reset time for
both the 5-hour and weekly windows:

```
5h ● 32% left · resets in 53 min      wk ● 59% left · resets Sat
```

Clicking it opens a detail panel with per-model weekly caps, exact reset times, current status,
and the running version. Threshold alerts raise a desktop notification once per window.

## How it works

Bingo reads your quota from the same endpoint Claude Code's own `/usage` command uses,
authenticating with the OAuth token Claude Code already maintains on your machine. It does not
ask for an API key, and no plan limits are hardcoded anywhere in the codebase — the percentages
come from the server, not from a guess about your plan.

Two consequences worth stating plainly:

- **That endpoint is undocumented.** Its payload has changed shape more than once. Bingo parses
  it tolerantly and, when it can't recognise what came back, says so and shows nothing rather
  than displaying a stale or estimated number. A readout you trust at a glance is worse than
  useless if it can quietly lie.
- **Nothing leaves your machine.** The only network call is to the quota endpoint. Your token is
  never logged, copied, or transmitted anywhere else, and there is no telemetry.

## Requirements

- Windows 10 or 11
- An active Claude Max or Pro subscription
- Claude Code installed and signed in — Bingo reads its credentials and delegates token refresh
  to it

## Repository layout

| Path | Contents |
|---|---|
| `specs/` | Feature specifications, technical plans, and task lists |
| `specs/memory/constitution.md` | Architectural principles applied across the project |
| `docs/research/` | Background research, including a teardown of three prior-art usage monitors |

## Architecture

Two projects with one rule between them: `BingoHud.Core` decides, `BingoHud.App` draws.

- **`BingoHud.Core`** — polling, response parsing, threshold state, reset countdowns. No UI
  dependency, so the logic most likely to harbour bugs is the part under test.
- **`BingoHud.App`** — WPF shell. HUD window, detail panel, tray, notifications. Renders what
  Core says and holds no logic of its own.

If UI code starts making decisions, that decision belongs in Core. The poll policy and severity
evaluation are pure functions, and time is injected throughout Core, so cadence and staleness are
deterministic under test.

## Building

_No solution scaffolded yet. Once it is, the commands will be `dotnet build`, `dotnet test`, and
`dotnet publish -c Release -r win-x64 --self-contained`. This section gets the real, verified
invocations at that point — not before._

## Versioning

Semantic versioning. Bingo stays on `0.x` until it has proven itself in daily use; because it
depends on an undocumented endpoint, anything below `1.0.0` should be treated as liable to break
when that endpoint changes.

- **PATCH** — bug fixes, no behaviour change a user would notice.
- **MINOR** — new behaviour, new settings, or any change to how the upstream response is parsed.
  Endpoint-handling changes are always at least MINOR, because that is the axis along which this
  app breaks.
- **MAJOR** — reserved for 1.0.0 and beyond.

Three things move together on every release, and a release is not done until all three agree:

1. `<Version>` in `BingoHud.App.csproj` — what the running app reports in its detail panel.
2. A dated section in `CHANGELOG.md`, moved down out of `[Unreleased]`.
3. A git tag `vX.Y.Z` on the release commit.

The version earns its keep beyond bookkeeping: when the upstream payload drifts, the first
question is which build is misreading it. A version the app displays and a changelog that
explains it is what makes that answerable. Record user-visible changes under `[Unreleased]` as
they land.

## Prior art

Three shipping usage monitors informed this design, and
[`docs/research/usage-monitor-teardown.md`](docs/research/usage-monitor-teardown.md) records what
was learned from reading them: the endpoint contract, the error taxonomy, the credential handling,
and the display conventions all three converged on independently.

## License

MIT — see [LICENSE](LICENSE).
