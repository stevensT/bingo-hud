# Spike: Codex usage endpoint

**Status:** open, 2026-11-03
**Script:** `scripts/capture-codex-usage.js`, to be written in step 2 and deleted on closing

## Question

Can Bingo get the account-wide Codex figure from the endpoint Codex itself calls, using the
sign-in Codex already keeps on the machine, without sending a Codex turn and without refreshing
Codex's token? And does that still work on a machine where Codex has not been opened for a day or
more?

## Why this spike exists

The usage-elsewhere spike (`usage-elsewhere.md`) showed the session logs catch up with usage from
another machine, but only on the next Codex turn on this one. Until then Bingo shows this machine's
last figure, correctly aged. Trevor moves between several machines and devices and wants to see how
much Codex usage is left before starting a project, wherever he is, without sending a turn first.
That is the second row of that spike's decision criteria, so this spike opens.

The same run showed the Codex app on the laptop picking up the desktop's usage without a turn
(`usage-elsewhere.md`, run 1, step 3). So an account-wide figure is reachable from this machine
without a turn. What is not known is whether Bingo can reach it, and at what cost.

The riskiest assumption is the sign-in, not the endpoint. Codex's access token expires. Bingo does
not refresh Codex's token (`spec.md`, Non-Goals), just as it leaves Claude's to Claude Code. If the
token only stays fresh while the Codex app runs, the endpoint fails on exactly the machines Trevor
wants to check before starting: the ones where he has not used Codex lately. That is cheap to find
out and expensive to find out after building the reader.

Taking this route reverses settled choices. AC-1 says Bingo makes no network call for Codex and
does not read Codex's token, and the README's "nothing leaves your machine" names Claude's endpoint
as the only network call. Both would change. Bingo already does the same thing for Claude, so this
is a second instance of an accepted pattern rather than a new kind of risk, but it is a decision to
make on evidence.

## What is known before the experiment

- Codex keeps its sign-in in `%USERPROFILE%\.codex\auth.json` (or under `CODEX_HOME`). That file
  holds an access token, a refresh token, and an ID token that carries the account's identity. None
  of its contents have been read for this spike.
- Codex is open source. The endpoint it calls for usage, the headers it sends, and the shape of the
  response can be read from its source rather than from network traffic. Until that is done, any
  endpoint named here is a guess and is not recorded as fact.
- The session logs' rate-limit record has a five-hour `primary` and a weekly `secondary` window,
  each with a used percentage and a reset time (`session-log-probe.md`). The endpoint is expected
  to report the same windows, possibly in a different shape.

## Method

1. **Find the endpoint in Codex's source.** Record the URL, the method, the headers it sends
   (including any account identifier besides the bearer token), which `auth.json` fields it reads,
   and the response shape the source expects. Note the Codex version and commit read. If the source
   shows how long an access token lasts or when Codex refreshes it, record that too.
2. **A capture script,** `scripts/capture-codex-usage.js`, modeled on `scripts/capture-usage.js`.
   Run by hand. It:
   - reads `auth.json` in memory and takes only the fields step 1 says the call needs
   - prints the access token's expiry and age, and `auth.json`'s last-refresh time if it has one,
     never the token or any identity claim
   - makes one request and prints the HTTP status, the rate-limit fields, and the response's
     key paths and types (never its values otherwise, so an error body is described, not quoted)
   - never writes to `auth.json` and never calls a token refresh, and prints whether
     `auth.json`'s modified time changed during the run
   - writes no files. Fixtures are captured by the reader's own task if the route is chosen.
3. **Fresh token, agreement.** Right after a Codex turn on this machine, read the Codex app, then
   run the script with nothing in between. Repeat at least three times across two days.
4. **Usage from elsewhere.** Use Codex on the desktop or the phone, then on the laptop, without a
   turn, read the app and run the script. Bingo's log reading is expected to lag; the endpoint is
   expected not to.
5. **Idle machine, the riskiest case.** On a machine where the Codex app has not run for at least
   24 hours, and with the app still closed, run the script. Record the token's expiry and age as
   printed, the status, and the figures. If the token's printed expiry has passed, this is the run
   that matters most. If it fails, record the error body's shape (it is the auth-failure case the
   reader would have to handle), then open Codex and run the script again.
6. **No side effects on Codex.** After steps 3 to 5, open Codex on each machine used. It is still
   signed in and its figures are unchanged by the calls. `auth.json`'s modified time is unchanged by
   the script.

Record each run under Findings: time, machine, how long since Codex last ran there, token age and
expiry, status, the script's figures, and the app's.

### Privacy

`auth.json` holds the refresh token and the account's identity, both worth more than the access
token. The script keeps only what the call needs, prints none of it, and writes none of it. The
response is printed as rate-limit fields only. Nothing the script prints is committed except
scrubbed fixtures; findings are summarised into this document by hand.

## What this does not test

- **Polling.** A handful of calls says nothing about whether calling every few minutes from two
  machines draws a rate limit or attention. If the route is chosen, the plan sets a cadence no
  faster than Claude's and the first week of use is watched.
- **Refreshing Codex's token.** Out of scope, and a Non-Goal. If refresh tokens rotate, Bingo
  refreshing would sign Codex out on that machine. If step 5 shows it would be needed, that is a
  separate decision with its own spike.
- **Terms of use.** Whether calling the endpoint outside Codex is acceptable to OpenAI. Bingo
  already takes this position for Claude; it is recorded as an open question, not answered here.
- **The display.** AC-3b, the remote icon, and how a log reading and an endpoint reading sit
  together on screen belong to the spec change that follows, if one does.
- **Accounts other than this one**, API-key sign-in, or other plans.

## Deliberate deviations from the constitution

**Principle 1, test-first, is suspended.** The script is throwaway and its output is a decision.
A reader that follows gets tests first, built on fixtures scrubbed from what this spike captures.

**Principle 6 stays in force.** The script prints the age of the token it used and the time of the
response, and Bingo's display is unchanged by this spike.

## Decision criteria

| Observed | Reading | Consequence |
|---|---|---|
| Steps 3 and 4 agree with the app within a point, step 5 succeeds with the app closed, step 6 shows no side effects | The endpoint gives the account figure on any signed-in machine | Plan the endpoint route: amend the spec (AC-1 reversed, AC-3b back), rewrite the README's network section. Whether the logs stay as a fallback is a plan decision. |
| Steps 3 and 4 agree, but step 5 fails once the token has expired and succeeds after opening Codex | The endpoint works only where Codex has run recently | Raise with Trevor: endpoint when the token is fresh and logs otherwise, with the detail panel saying Codex needs opening, or a separate spike on refreshing. Not decided here. |
| The endpoint's figures disagree with the app by more than a point, or its windows do not match the logs' | The endpoint is not the figure Codex shows | Stop. The logs stay the only source. |
| The call needs something Bingo cannot supply without acting as Codex beyond the bearer token and an account header, or the source shows no usage endpoint | No usable route | Stop. The logs stay; record what was found. |
| Step 6 shows Codex signed out or its sign-in changed | The call disturbs Codex | Stop at once and record it. The route is ruled out. |

## Exit

When the spike closes, record the result below and delete `scripts/capture-codex-usage.js`. Any
fixtures it wrote are kept only if the endpoint route is chosen.

## Findings

### Step 1, 2026-11-03: the endpoint in Codex's source

Read from `openai/codex` at commit `3a2788e` (main, 2026-11-04 05:25 UTC). Files:
`codex-rs/backend-client/src/client.rs` and `client/rate_limit_resets.rs`,
`codex-rs/login/src/auth/manager.rs`, `storage.rs` and `token_data.rs`,
`codex-rs/http-client/src/openai_headers.rs`, and the response models under
`codex-rs/codex-backend-openapi-models/src/models/`.

**The call.**
- `GET https://chatgpt.com/backend-api/wham/usage` for a ChatGPT sign-in. (An `/api/codex/usage`
  path exists for a different base URL; not this case.)
- Headers: `Authorization: Bearer <access token>`, `chatgpt-account-id: <account id>`, and a
  `User-Agent`. Codex sends its own; with none set the client falls back to `codex-cli`. An
  optional `x-openai-codex-luna-reserve: 1` is sent only by clients that can apply a reserve; the
  source comments that it is "not for passive account usage readers", so Bingo would not send it.
- Nothing else. No body, no signing, no client secret. That rules out the fourth decision row.

**What it needs from `auth.json`.** Two fields under `tokens`: `access_token` (a JWT) and
`account_id`. The file also holds `refresh_token`, `id_token` (identity, including email), and a
top-level `last_refresh` time. The script needs neither of the last two, and reads `last_refresh`
only to print it.

**The response, as the source models it.**
- `plan_type`
- `rate_limit`: `allowed`, `limit_reached`, `primary_window` and `secondary_window`, each with
  `used_percent` (integer), `limit_window_seconds`, `reset_after_seconds` and `reset_at` (Unix
  seconds)
- also `credits`, `spend_control`, `additional_rate_limits` (separate per-model limits, each with
  its own windows), `rate_limit_reached_type`, `rate_limit_reset_credits`
- Same two windows as the logs' `primary` and `secondary`, renamed, with the window length in
  seconds rather than minutes. `used_percent` is an integer here; the logs have carried a float.

**When Codex refreshes its token.** In `manager.rs`, Codex refreshes when the access token's JWT
expiry is within 5 minutes, or, if the expiry cannot be read, when `last_refresh` is more than
8 days old. It also refreshes and retries once on a 401. The source does not say how long an access
token lasts; the JWT's own expiry does, and the script will print it.

**Refresh tokens are single-use.** The refresh failure codes include `refresh_token_reused`, with
the message "your refresh token was already used. Please log out and sign in again." So if Bingo
refreshed the token and did not write the new pair back to `auth.json`, Codex's next refresh would
fail and sign it out. Writing it back would mean Bingo taking over Codex's sign-in. The Non-Goal is
not just caution: refreshing is ruled out unless Codex's own refresh is triggered somehow.

**On this machine.** `auth.json` exists, 4,280 bytes, last written 2026-11-03 20:40 (checked by
file properties only, contents not read). Codex ran here tonight, so its token is fresh; step 5
needs a machine idle for longer.

**What this changes for step 2.** The script reads `tokens.access_token` and `tokens.account_id`,
decodes the access token's `exp` claim without printing anything else from it, and prints
`last_refresh`. Open choice for Trevor before writing it: which `User-Agent` to send. Honest
(`bingo-hud/<version>`) is the default; Codex's own string would hide that the caller is not
Codex. The Claude capture script sends a Claude Code user agent as a fallback, so this choice
applies there too.

## Result

(open)
