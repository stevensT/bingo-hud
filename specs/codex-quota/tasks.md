# Codex Quota — Tasks

## Status Legend

- `[ ]` Not started
- `[x]` Complete
- `[~]` In progress
- `[P]` Parallelizable — executed concurrently as subagents, or inline when small
- `[C]` Checkpoint — stop and verify before continuing

Task ids are stable. Acceptance criteria from `spec.md` are cited per task.

Every Core task is test-first: write the test, run it and see it fail, then implement. The WPF
shell has no tests, so its tasks keep every decision in Core (tested in phases 1 to 4) and are
verified on screen, with Trevor, against the criterion each one names.

Steps marked **(Trevor)** are his to take or approve.

## Build and test commands

See the Building section of `README.md`. Every checkpoint runs `dotnet clean` first, then
`dotnet build` and `dotnet test`: 0 warnings, 0 errors, no failures.

---

## Phase 1: Fixtures and the record parser

- [x] 1.1 Cut fixtures from the real logs into `tests/fixtures/codex/`, each a dated `.jsonl`.
      Every record line is reduced to `timestamp`, `type`, `payload.type` and
      `payload.rate_limits`; nothing else from the user's logs is copied. Captured, read-only, by
      a one-off command that is not committed:
      - a session with several records with figures, from `Codex Desktop 0.162.0-alpha.17.2`;
      - a record from `codex-tui 0.162.1`;
      - a record with `rate_limits: null`.
      Hand-made, and labelled as such in the README: a record whose window length is neither 300
      nor 10080; one with a field missing; one with a field of the wrong type; one with a
      non-null `rate_limit_reached_type`. Decoy lines with invented conversation text are mixed
      into the session file, each containing a marker string, for the privacy test (2.1).
      `tests/fixtures/codex/README.md` says where each came from and what was removed. (AC-1)
      *Done 2026-10-14. The session came from `codex_work_desktop 0.162.0-alpha.17.2` rather than
      `Codex Desktop`: it is the one log that crosses a five-hour reset and shows `resets_at`
      moving eight seconds for one window, both cases later tasks need. One fixture added beyond
      the list, a log whose last line is cut off mid-write; 2.1 tests it.*
- [x] 1.2 Test `CodexRecord.Parse(line)`, against the fixtures:
      - a record with figures gives a `QuotaSnapshot` with `Session` and `WeeklyAll`, mapped by
        `window_minutes`, `used_percent` as sent, `resets_at` from Unix seconds, `ObservedAt`
        from the line's `timestamp`;
      - `RawBody` is the `rate_limits` object's text and nothing more;
      - a null `rate_limits` is "no reading", distinct from a snapshot and from unreadable;
      - an unknown window length is left out and named; a missing or wrongly typed field is
        unreadable with a reason;
      - `rate_limit_reached_type` is carried verbatim when not null.
      Run it red. (AC-1, AC-2)
      *Done 2026-10-14: 26 tests in `CodexRecordTests`, all red on the stub's
      `NotImplementedException`. Added beyond the list: decoy lines are "not a record"; a record
      with no window of a known length, or no usable timestamp, is unreadable; a cut-off line is
      unreadable rather than an exception; a trailing carriage return reads the same.*
- [x] 1.3 Implement `CodexRecord.Parse` until 1.2 passes.
      *Done 2026-10-14: 26 of 26 pass; full suite 1515 pass. Beyond the tests: two windows of
      the same length make the record unreadable, and a null `resets_at` is allowed, as for a
      Claude window, meaning no countdown.*
- [x] 1.4 Checkpoint: clean, build, full suite; record in `progress.md`. *Passed 2026-10-14.*

## Phase 2: Reading the logs

- [x] 2.1 Test `CodexLogReader.Scan()` against temp folders built from the fixtures:
      - the newest record with figures wins, newest file first, across `sessions\` and
        `archived_sessions\`;
      - a file holding only null records is passed over for the next one;
      - the root is `CODEX_HOME` when given, else `%USERPROFILE%\.codex`;
      - no root folder is "not found"; a root with no record is "no reading yet";
      - a file that cannot be opened is a problem with a reason, not an exception;
      - a last line cut off mid-write is passed over, and the record before it is the reading:
        Codex appends while Bingo reads, so this is ordinary, not a problem;
      - files checked out with CRLF line endings read the same as LF;
      - privacy: after scanning a file with decoy lines, no decoy marker string appears in
        anything the scan returns, `RawBody` included.
      Run it red. (AC-1, AC-7)
      *Done 2026-10-14: 18 tests in `CodexLogReaderTests`, all red on the stub. A line still being
      written is told by its missing final newline: all 35 real logs end in one (checked by
      reading each file's last byte only). Added beyond the list: a null record after figures
      does not hide them; a newest record in an unknown shape is a problem, not a fall-back to an
      older figure; files elsewhere in the Codex folder are not read.*
- [x] 2.2 Implement `CodexLogReader` until 2.1 passes.
      *Done 2026-10-14: 18 of 18 pass; full suite 1533 pass. Logs are opened with read and
      delete sharing, so Codex can go on writing a session's log while Bingo reads it. The
      privacy test was strengthened to prove its serialized text reaches the kept record.*
- [ ] 2.3 Test `CodexMonitor`:
      - `Rescan()` reads the logs only when a log file's last write time has moved since the last
        scan;
      - a failed scan keeps the last reading and records the problem (AC-3a failed); a clean scan
        with no newer record keeps the reading and clears the problem (AC-3a idle);
      - the reading's age is measured from its `ObservedAt`, not from the scan (AC-3).
      Run it red.
- [ ] 2.4 Implement `CodexMonitor` until 2.3 passes.
- [C] 2.5 Checkpoint: clean, build, full suite. Also time one `Scan()` against the real
      `%USERPROFILE%\.codex`, read-only, and record it; over one second is raised before
      continuing. Record in `progress.md`.

## Phase 3: Products in the model

The three units touch separate files and do not depend on each other.

- [P] 3.1 Test + implement, in `Alerts/`: `Product` (Claude first); `AlertKey` with a product;
      a key counts as fired when a fired key for the same product, window and threshold resets
      within ten minutes of it; `AlertEngine` takes the product with the snapshot.
      `AlertStateStore` reads a file in 0.4.0's exact shape, with no product, as Claude, and
      keeps pruning by reset. Tests first: two Codex readings of one window with resets 8 seconds
      apart raise one alert; Claude and Codex alerts for the same kind of window are independent;
      a 0.4.0 file round-trips with its alerts still fired. (AC-10)
- [P] 3.2 Test + implement, in `Settings/`: `ProductSwitches(Claude, Codex)`;
      `UserSettings.Products`, null until answered; `SettingsStore` writes and reads it, and a
      0.4.0 file with no products loads as null, not as a default. Turning off the last product
      that is on is refused. Tests first. (AC-8, AC-8a)
- [P] 3.3 Test + implement `ProductDetection`: given a user profile folder and an optional
      `CODEX_HOME`, which products are found, and the suggested ticks (found ticked, missing
      unticked). Tests first, against temp folders. (AC-8b)
- [C] 3.4 Checkpoint: clean, build, full suite; record in `progress.md`, saying how the
      parallel group was run and why.

## Phase 4: Display in Core

- [ ] 4.1 Test, before any display change: a Claude-only HUD, for every state the current
      `Readout` tests cover, has the same lines, marks and phrases as today except that each line
      reads "Claude 5h" or "Claude Week". Run it red on the label alone. (AC-9, AC-7a)
- [ ] 4.2 Test + implement `HudContent` as product sections and `Readout` per product: every line
      labelled with its product; collapse applies within each product; the accent severity is
      the worst live line across products; a product switched off has no section. 4.1 passes.
      (AC-4, AC-5, AC-7a, AC-8)
- [ ] 4.3 Test + implement the Codex section from `CodexState`:
      - not found and no reading yet each give a phrase in place of lines (AC-7);
      - a reading always carries its age, "used 12m ago" (AC-3);
      - idle keeps its colours; a failed scan marks "logs unreadable" and drops them (AC-3a);
      - a window whose reset has passed shows its name and "reset, no use since", no figure, no
        bar, normal severity (AC-3, AC-2).
- [ ] 4.4 Test + implement `StatusMessage` words for the Codex states: headline and advice for
      not found, no reading yet, logs unreadable and unknown shape. Advice says what to do:
      install or open Codex, use it once, or that a Codex update is the likely cause. (AC-7)
- [P] 4.5 Test + implement `PanelReadout` per product: a block per shown product with exact
      resets and age; the Codex block says the reading is as of the last Codex turn on this
      machine and shows a non-null reached type verbatim. (AC-1, AC-6)
- [P] 4.6 Test + implement `AlertMessage` naming the product in every notification, and Codex
      alert evaluation that passes over a window whose reset has passed. (AC-10, AC-3)
- [C] 4.7 Checkpoint: clean, build, full suite; every Core criterion assessed against `spec.md`;
      record in `progress.md`.

## Phase 5: The shell

- [ ] 5.1 First-start window: shown before the HUD while `Products` is null, one tick box per
      product from `ProductDetection`, "not found" beside a missing one, Continue disabled with
      nothing ticked; the answer saved and used. If the save fails, the answer still holds for
      the session. Checked on screen with Trevor, using a copy of the settings file with no
      products. (AC-8a, AC-8b)
- [ ] 5.2 Tray: "Show Claude" and "Show Codex" check marks above the display options, the last
      one on cannot be cleared. Checked on screen. (AC-8, AC-8c)
- [ ] 5.3 HUD: draw product sections, each with its lines and its mark; the reset-passed line
      without figure or bar. Checked on screen with both products, with each alone, and with
      collapse on. (AC-4, AC-7a, AC-9)
- [ ] 5.4 Detail panel: one block per shown product. Checked on screen. (AC-6)
- [ ] 5.5 App wiring: `CodexMonitor` built with the real root; a 60 s timer calls `Rescan()`;
      "Refresh now" also rescans; Codex alerts evaluated after each rescan and announced through
      the same path as Claude's. (AC-1, AC-10)
- [ ] 5.6 On-screen states with Trevor, by pointing `CODEX_HOME` at a test folder for each:
      not found, no reading yet, a reading, logs unreadable, a window whose reset has passed.
      Then with the real logs: the Codex figure against the Codex app. (AC-3, AC-3a, AC-7)
- [C] 5.7 Checkpoint: clean, build, full suite; every acceptance criterion assessed; the trial
      settings restored; record in `progress.md`.

## Phase 6: Docs and review

- [ ] 6.1 README: rewritten as a readout for AI coding quotas with Claude and Codex as peers
      (spec Decided). "How it works" covers the Codex source, what it cannot see, and that
      nothing leaves the machine for Codex; Requirements, Privacy and Removing updated;
      screenshots retaken **(Trevor takes them)**.
- [ ] 6.2 CHANGELOG `[Unreleased]`: the Codex readout, the first-start question, the product
      check marks, and product names in notifications.
- [ ] 6.3 Run the review on the feature's changes and resolve or defer each finding, recording
      deferrals here.

## Build Verification

- [ ] BV.1 `dotnet clean` then `dotnet build`: 0 warnings, 0 errors.
- [ ] BV.2 `dotnet test`: full suite, no failures, no unexplained skips.
- [ ] BV.3 Both publish shapes build per the README's Publishing section, and the setup builds
      from the self-contained one.
- [ ] BV.4 Final checkpoint: all green, every acceptance criterion met (AC-3b deferred), recorded
      in `progress.md`. Release as 0.5.0 by the README's Releasing steps **(Trevor)**.
