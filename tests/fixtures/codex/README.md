# Codex session log fixtures

Rate-limit records cut from Codex's own session logs (`%USERPROFILE%\.codex\sessions\`), the
source Bingo reads Codex windows from. Like the usage fixtures beside them, they are dated and
their origin recorded, because they are the only honest way to test a reader against an
undocumented format.

**What was removed.** The logs hold the user's conversations. Only lines whose `type` is
`event_msg` with `payload.type` `token_count` were taken, and each was rebuilt from four fields:
`timestamp`, `type`, `payload.type`, and `payload.rate_limits`. The `rate_limits` object is the
original text, unchanged, so number formatting such as `44.0` is as Codex wrote it. Everything else
on those lines was dropped: `payload.info` (token counts) and, where present, `ordinal`. No
conversation line was copied. The records carry no account identifier; `plan_type` and the credits
balance are kept as written.

**Times.** `timestamp` is UTC (`Z`). `resets_at` is Unix seconds.

**Line endings.** Written with LF. Git may check them out with CRLF, so a reader has to accept both.

**When a test against these fails, recapture rather than loosen the reader.** A failing test is the
signal that Codex changed its record.

## Inventory

| Fixture | Kind | Contents |
|---|---|---|
| `2026-10-13-work-desktop-session` | captured, with decoys | Five records from one `codex_work_desktop 0.162.0-alpha.17.2` session, 2026-10-13 evening local time: the first (five-hour 44%), the last before a five-hour reset (50%), the first after it (0%, `resets_at` …796), the next (0%, `resets_at` …804, eight seconds later for the same window), and the last (24%). Four invented decoy lines are mixed in; see below. |
| `2026-10-14-tui-record` | captured | One record from `codex-tui 0.162.1`, the terminal client. |
| `2026-10-14-null-record` | captured | One record with `rate_limits: null`, from a log the Codex app wrote as it started. |
| `2026-10-14-made-unknown-window` | hand-made | The tui record with `secondary.window_minutes` changed to 1440, a length Bingo does not know. |
| `2026-10-14-made-missing-field` | hand-made | The tui record with `primary.used_percent` removed. |
| `2026-10-14-made-wrong-type` | hand-made | The tui record with `primary.used_percent` written as the string `"24.0"`. |
| `2026-10-14-made-reached-type` | hand-made | The tui record with `primary.used_percent` 100 and `rate_limit_reached_type` `"primary"`. Never observed non-null; the value is invented. |
| `2026-10-14-made-truncated-tail` | hand-made | The tui record, then a second record cut off mid-line, as a log being written while it is read ends. |

### Decoy lines

The session fixture holds four invented lines that stand in for conversation, each carrying a
marker string beginning `DECOY-MARKER-`. Three of them contain the text `rate_limits`, so they pass
a text prefilter and must be rejected by structure: a user message, an agent message, and a tool
output whose text is itself a JSON object with a `rate_limits` key and a 99% figure. The fourth
mentions nothing of the kind. No marker string may appear in anything the reader returns.

### Hand-made fixtures

The hand-made fixtures exist because the cases they cover cannot be produced on demand from a live
account. Each is one deliberate change to a captured record, named in the table, and is named
`made` so it cannot be mistaken for a capture.

## Recapturing

There is no committed capture script. The 2026-10-14 captures were cut by a one-off, read-only
command that kept the four fields above. Any recapture must keep to the same rule: no line other
than a `token_count` record, and no field other than those four.
