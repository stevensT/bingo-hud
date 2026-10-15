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
      - the newest record with figures wins, ~~newest file first~~ by record timestamp (corrected
        at 5.6), across `sessions\` and
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
- [x] 2.3 Test `CodexMonitor`:
      - ~~`Rescan()` reads the logs only when a log file's last write time has moved since the last
        scan~~; corrected at 5.6: every requested rescan reads, because Windows file times can lag;
      - a failed scan keeps the last reading and records the problem (AC-3a failed); a clean scan
        with no newer record keeps the reading and clears the problem (AC-3a idle);
      - the reading's age is measured from its `ObservedAt`, not from the scan (AC-3).
      Run it red.
      *Done 2026-10-14: 15 tests in `CodexMonitorTests` and 2 for `CodexLogReader.NewestWrite`,
      all red on the stubs. Added beyond the list: after a failed scan the logs are read again
      even with nothing written, since a released lock writes nothing; Codex not found clears an
      earlier reading (AC-7); one test runs the whole path over a real folder, appending a newer
      record as Codex does at the end of a turn. `CodexState` and `CodexPresence` added as the
      monitor's interface.*
- [x] 2.4 Implement `CodexMonitor` until 2.3 passes.
      *Done 2026-10-14: full suite 1551 pass. One rule went in beyond 2.3's tests: while Codex is
      not found, every rescan looks again, because installing Codex creates its folder before
      any log. Its test was written after the code, so it was seen to fail with the rule removed
      before being kept.*
- [x] 2.5 Checkpoint: clean, build, full suite. Also time one `Scan()` against the real
      `%USERPROFILE%\.codex`, read-only, and record it; over one second is raised before
      continuing. Record in `progress.md`. *Passed 2026-10-14: first scan 58 ms over 42 logs
      (30.4 MB).*

## Phase 3: Products in the model

The three units touch separate files and do not depend on each other.

- [x] 3.1 Test + implement, in `Alerts/`: `Product` (Claude first); `AlertKey` with a product;
      a key counts as fired when a fired key for the same product, window and threshold resets
      within ten minutes of it; `AlertEngine` takes the product with the snapshot.
      `AlertStateStore` reads a file in 0.4.0's exact shape, with no product, as Claude, and
      keeps pruning by reset. Tests first: two Codex readings of one window with resets 8 seconds
      apart raise one alert; Claude and Codex alerts for the same kind of window are independent;
      a 0.4.0 file round-trips with its alerts still fired. (AC-10)
      *Done 2026-10-14: 13 tests in `AlertProductTests`; 11 seen red, then green. Two passed
      before any change and are kept as guards: the product written by name, and a 0.4.0 file
      read as Claude's (the key's product is last and defaults to Claude, which is also why the
      39 existing call sites did not change). `Product` lives in Core's root namespace. The test
      store matches with the same tolerance as the real one. Full suite 1564 pass.*
- [x] 3.2 Test + implement, in `Settings/`: `ProductSwitches(Claude, Codex)`;
      `UserSettings.Products`, null until answered; `SettingsStore` writes and reads it, and a
      0.4.0 file with no products loads as null, not as a default. Turning off the last product
      that is on is refused. Tests first. (AC-8, AC-8a)
      *Done 2026-10-14: 11 tests in `ProductSettingsTests`; 7 seen red, then green, and 4 kept as
      guards that passed before the store read products (no answer by default, no file, a 0.4.0
      file, and an answer with nothing on, which reads as no answer so the question is asked
      again). A hand-edited answer naming one product reads the other as off. Full suite 1575.*
- [x] 3.3 Test + implement `ProductDetection`: given a user profile folder and an optional
      `CODEX_HOME`, which products are found, and the suggested ticks (found ticked, missing
      unticked). Tests first, against temp folders. (AC-8b)
      *Done 2026-10-14: 7 tests in `ProductDetectionTests`, all seen red, then green. Codex is
      looked for with the same root the log reader uses, so the question and the HUD cannot
      disagree about whether Codex is installed.*
- [x] 3.4 Checkpoint: clean, build, full suite; record in `progress.md`, saying how the
      parallel group was run and why. *Passed 2026-10-14.*

## Phase 4: Display in Core

- [x] 4.1 Test, before any display change: a Claude-only HUD, for every state the current
      `Readout` tests cover, has the same lines, marks and phrases as today except that each line
      reads "Claude 5h" or "Claude Week". Run it red on the label alone. (AC-9, AC-7a)
      *Done 2026-10-14: 15 tests in `ClaudeOnlyHudTests`. The expectations were recorded from
      0.4.0's `Readout` by a throwaway program run over the same 15 states, then given the label;
      bar fills come from `Bar.Segments`, which this feature does not change. 12 reading tests
      red on the window label and nothing else; the 3 empty-phrase tests pass, since the phrases
      do not change, and are kept as guards. Decided here: a Claude-only empty phrase is
      unchanged ("No reading yet"), and a section's product is drawn by the shell from the
      section rather than written into the phrase.*
- [x] 4.2 Test + implement `HudContent` as product sections and `Readout` per product: every line
      labelled with its product; collapse applies within each product; the accent severity is
      the worst live line across products; a product switched off has no section. 4.1 passes.
      (AC-4, AC-5, AC-7a, AC-8)
      *Done 2026-10-14: 13 tests in `HudCompositionTests`, red then green; the 15
      `ClaudeOnlyHudTests` now ask through `Readout.Compose` and pass with their expectations
      unchanged. Shape: `HudContent` is kept as one section's content, and a new `Hud` holds the
      sections with the accent and the repaint check, which kept `Readout.Content` and its tests
      in place. Both products' lines come from one builder. The 14 label assertions in
      `ReadoutTests` gained "Claude " and nothing else. Before the question is answered, only
      Claude is shown. Codex's empty phrases are placeholders marked `deferred:` until 4.4. Full
      suite 1610.*
- [x] 4.3 Test + implement the Codex section from `CodexState`:
      - not found and no reading yet each give a phrase in place of lines (AC-7);
      - a reading always carries its age, "used 12m ago" (AC-3);
      - idle keeps its colours; a failed scan marks "logs unreadable" and drops them (AC-3a);
      - a window whose reset has passed shows its name and "reset, no use since", no figure, no
        bar, normal severity (AC-3, AC-2).
      *Done 2026-10-14: 13 tests in `CodexSectionTests`; 10 red then green, 3 passing before as
      guards (4.2's placeholder phrases already differ, and the other window's figure is
      ordinary behaviour). Marks: "used 12 min ago", "used just now", and with a failed look
      ", logs unreadable" or ", log format not recognised", worded in `StatusMessage.CodexMark`
      beside Claude's marks. A reset at exactly now has passed. Collapse counts an ended window
      as normal and emptiest, so it keeps the other. `ReadoutLine.Percent` is now optional; the
      whole solution, app included, builds with 0 warnings. Full suite 1623.*
- [x] 4.4 Test + implement `StatusMessage` words for the Codex states: headline and advice for
      not found, no reading yet, logs unreadable and unknown shape. Advice says what to do:
      install or open Codex, use it once, or that a Codex update is the likely cause. (AC-7)
      *Done 2026-10-14: 8 tests in `CodexStatusMessageTests`, all red then green. Headlines:
      "Looking for Codex" (before the first look), "Codex not found", "No Codex reading yet",
      "Codex logs unreadable", "Codex log format not recognised". A failed look outranks the
      rest; a clean reading has nothing to say. Not found offers both ways out, install or
      switch it off in the tray. Unreadable logs say Bingo will retry; an unrecognised record
      says a wait will not fix it. The HUD's empty Codex phrase is now the headline, which
      removed 4.2's `deferred:` placeholder. Full suite 1631.*
- [x] 4.5 Test + implement `PanelReadout` per product: a block per shown product with exact
      resets and age; the Codex block says the reading is as of the last Codex turn on this
      machine and shows a non-null reached type verbatim. (AC-1, AC-6)
      *Done 2026-10-14: 18 tests in `CodexPanelTests`, all red then green. Claude's part is
      composed exactly as before and hidden by the shell when switched off; `PanelContent` gains
      `ShowClaude` and an optional `CodexPanel`, composed by `PanelReadout.ComposeAll`. The Codex
      block always states its source and what it cannot see. A window whose reset has passed
      shows "no reading since it reset" and the time it reset. Full suite 1649.*
- [x] 4.6 Test + implement `AlertMessage` naming the product in every notification, and Codex
      alert evaluation that passes over a window whose reset has passed. (AC-10, AC-3)
      *Done 2026-10-14: 9 tests in `CodexAlertTests`, all red then green. Toasts now read "Claude
      5h window running low" and "Codex Week window nearly used up", and batch lines name each
      window's product; the 7 existing toast assertions gained "Claude " and nothing else.
      `CodexAlerts.TakeNew` hands the engine only Codex windows whose reset has not passed, a
      reset at exactly now counting as passed, so the engine itself is unchanged. Full suite
      1658.*
- [x] 4.7 Checkpoint: clean, build, full suite; every Core criterion assessed against `spec.md`;
      record in `progress.md`. *Passed 2026-10-14.*

## Phase 5: The shell

- [x] 5.1 First-start window: shown before the HUD while `Products` is null, one tick box per
      product from `ProductDetection`, "not found" beside a missing one, Continue disabled with
      nothing ticked; the answer saved and used. If the save fails, the answer still holds for
      the session. Checked on screen with Trevor, using a copy of the settings file with no
      products. (AC-8a, AC-8b)
      *Done 2026-10-14. Words, starting ticks, the Continue rule and the closed-without-answer
      fallback are `FirstStartContent` in Core (7 tests, red then green); the window draws them.
      Shutdown is explicit while the question is up, because WPF makes the first window the main
      window and the app quits when its main window closes; startup restores it once the HUD is
      main. `CODEX_HOME` is read through `CodexLogReader.HomeVariable`, the one environment
      variable the shell fence now allows outside Debug, with a test that it is the only one;
      `FirstStartWindow` joins the list of windows that take a handle, for the dark title bar.
      On screen with Trevor, real settings backed up to `artifacts\settings-backup-5.1` first:
      the question came before any HUD with both products ticked and no "not found"; unticking
      both disabled Continue; Continue saved both and the HUD appeared without Bingo quitting;
      after restoring, closing with the X still brought up the HUD and saved nothing. Found on
      screen: the saved answer carried a derived "anyOn"; a test pinned it red, and it is now
      kept out of the file. Full suite 1667. Real settings restored and unchanged.*
- [x] 5.2 Tray: "Show Claude" and "Show Codex" check marks above the display options, the last
      one on cannot be cleared. Checked on screen. (AC-8, AC-8c)
      *Done 2026-10-14. Items flip through `ProductSwitches.With`; on opening, each is checked
      from the settings and the last one on is greyed out. On screen with Trevor: both appear
      checked above the display options; clicking "Show Claude" unchecks it and greys out "Show
      Codex"; clicking it again restores both. Saved file checked after each step.
      Unexplained, from the first run: two clicks changed the in-memory setting, so the menu
      showed them, but neither reached the file, and nothing said so. Two later runs with a
      temporary log in `Remember` (since removed) saved every change, including through the
      first-start path, so it did not reproduce. Likeliest cause is a brief lock on the file,
      which `Save` swallows by design: the existing `deferred:` note on `Remember` in
      `App.xaml.cs`. Raised with Trevor rather than fixed here. Also seen: the HUD saved a
      one-pixel position change on its own, three seconds after a click.*
- [x] 5.3 HUD: draw product sections, each with its lines and its mark; the reset-passed line
      without figure or bar. Checked on screen with both products, with each alone, and with
      collapse on. (AC-4, AC-7a, AC-9)
      *Done 2026-10-14. All sections in one grid so figures align across products, with a small
      gap between products; an empty section is labelled with its product beside its phrase; an
      ended line draws no figure. Building the Codex monitor and its one-minute rescan, off the
      UI thread, moved here from 5.5, since Codex cannot be seen without them. The time-zone
      fence now follows `Readout.Compose`. On screen with Trevor: both sections, Codex figures
      matching the Codex app; each product alone; collapse within each. At Trevor's request a
      live normal Codex bar is blue rather than Claude orange, severity colours shared; recorded
      in the spec's Decided list. Settings restored.*
- [x] 5.4 Detail panel: one block per shown product. Checked on screen. (AC-6)
      *Done 2026-10-14. A CLAUDE block (status, windows, per-model caps, its own facts) hidden
      when Claude is off; a CODEX block (status, windows, Reading, Last turn, limit reached and
      unknown windows when present, the source note); Version and Refresh now below both. The
      app calls `PanelReadout.ComposeAll`, and the time-zone fence follows it. On screen with
      Trevor: both blocks; Claude off shows Codex alone with no divider; Codex off shows Claude
      alone. Found on screen, with Claude's five-hour window at its limit: the single HUD accent
      ran down beside Codex too. Changed to one accent per product, beside that product's lines
      only; `Hud.Overall` removed and its tests rewritten per section; recorded in the plan's Key
      Decisions; checked on screen. Also seen: the installed 0.4.0, running alongside, rewrote
      the settings file without the products answer, so the question came back; an artefact of
      two versions sharing one file, not a defect.*
- [x] 5.5 App wiring: ~~`CodexMonitor` built with the real root; a 60 s timer calls `Rescan()`;~~
      *(moved into 5.3)* "Refresh now" also rescans; Codex alerts evaluated after each rescan and announced through
      the same path as Claude's. (AC-1, AC-10)
      *Done 2026-10-15. Startup, the minute timer and Refresh now share the rescan-and-alert path;
      initial logs are read before the HUD, alerts taken only after the tray exists. Mute covers
      both products and is enabled with a Codex-only reading. Review found overlapping product
      notifications could be dropped: delivery now waits for the current notification to close
      or be clicked, then combines waiting crossings. Review also found mute must discard pending
      crossings; fixed. Six delivery tests were seen red before implementation, then green;
      full suite 1673 pass, build 0 warnings/errors. Code, test, type, comment and simplification
      reviews completed; both behavior findings and comment corrections resolved, no deferrals.*
- [x] 5.6 On-screen states with Trevor, by pointing `CODEX_HOME` at a test folder for each:
      not found, no reading yet, a reading, logs unreadable, a window whose reset has passed.
      Then with the real logs: the Codex figure against the Codex app. (AC-3, AC-3a, AC-7)
      *Done 2026-10-15. Trevor confirmed all five synthetic states. Real comparison found
      a source bug: 75% remaining in the app, 16% used in Bingo. Live log records were newer than
      their files' modification times. Regressions first, then removed file-time ordering and
      the monitor's file-time skip; records ordered by their own timestamps, unreadable records
      retain their timestamp when possible. Full scans 115–167 ms. Full suite 1673 pass, build
      0 warnings/errors. Review completed, comment corrections resolved; a repeated real comparison
      agreed for both windows. Both synthetic product notifications appeared in sequence, confirming
      the delivery callbacks from 5.5. Shared files restored and installed build restarted.*
- [x] 5.8 A settings save that fails is retried once and then said, not dropped. *Added
      2026-10-14, Trevor's call, after 5.2's first on-screen run, where two tray clicks never
      reached the settings file and nothing said so. Lifts the `deferred:` note on `Remember` in
      `App.xaml.cs`. Placed before the checkpoint because it touches the panel 5.4 draws.* (AC-8)
      - Test + implement in Core: `SettingsStore.Save` tries once more after a short pause when
        the file is busy. Tests first: a file held for the whole attempt still returns false,
        after two tries; a file released during the pause is saved.
      - Test + implement in Core: the panel carries a settings notice of its own, independent
        of either product's reading. A failed save sets it, saying the change holds until Bingo
        closes and naming the file; the next successful save clears it.
      - Shell: `Remember` reports the result to the notice; the panel draws it. Checked on screen
        by holding the settings file open from PowerShell while clicking a tray item, with
        Trevor, then releasing it and clicking again.
      *Stopped 2026-10-15, still in progress. Retry, notice composition, redraw comparison and
      shell wiring implemented. Eight new tests seen red before implementation, one success
      guard passed before it; full suite 1683 pass / 0 fail / 0 skip. Code, failure-handling,
      test, type, comment and simplification reviews completed; one panel summary corrected.
      On screen, Trevor changed Show bar but saw no save-failure notice. Cause unresolved;
      the diagnostic call was interrupted before its results were assessed. Next: reproduce with
      an independently confirmed exclusive settings lock and correct dev instance, trace the save
      result through the panel, verify notice appearance and clearing. Trial ended; both shared
      files restored exactly from artifacts/settings-backup-5.8; installed 0.4.0 restarted.*
      *Done 2026-10-15. No code change. Repeated with the installed 0.4.0 stopped, so only the
      dev build's tray icon existed, and with the lock held by a separate process and confirmed
      by a refused write-open. Trevor saw the notice naming the file after a tray change. The
      lock was released, the next change saved, and the notice cleared; the file then held the
      products answer given while locked, so held changes reached disk. The first run's missing
      notice is put down to the trial, most likely a click on 0.4.0's identical tray icon, which
      has no notice. Shared files restored exactly from artifacts/settings-backup-5.8b; 0.4.0
      restarted.*
- [x] 5.7 Checkpoint: clean, build, full suite; every acceptance criterion assessed; the trial
      settings restored; record in `progress.md`. *Passed 2026-10-15. The one gap, AC-8b's "not
      found" mark, was then checked on screen with Trevor.*

## Phase 6: Docs and review

- [x] 6.1 README: rewritten as a readout for AI coding quotas with Claude and Codex as peers
      (spec Decided). "How it works" covers the Codex source, what it cannot see, and that
      nothing leaves the machine for Codex; Requirements, Privacy and Removing updated;
      screenshots retaken **(Trevor takes them)**.
      *Done 2026-10-15. Rewritten as above; Privacy is the "Nothing leaves your machine"
      paragraph, there being no separate section. Requirements name a ChatGPT plan for Codex,
      matching `plan_type` in the fixtures. Screenshots taken by script rather than by Trevor,
      as at the display-options spike: a Debug build against the stand-in usage server restored
      from history to a scratch folder, a fake credential, and `CODEX_HOME` at a made-up record,
      so no real account figures are published. Shared files restored exactly from
      artifacts/settings-backup-6.1; 0.4.0 restarted. Raised, not changed: whether the
      Versioning section's at-least-MINOR rule covers the Codex log format.*
- [x] 6.2 CHANGELOG `[Unreleased]`: the Codex readout, the first-start question, the product
      check marks, and product names in notifications.
      *Done 2026-10-15. Beyond the four named: the per-product bar color and stripe, product
      labels on every HUD line, and 5.8's save retry and notice, all user-visible. The opening
      paragraph now names both undocumented sources, as the README's Versioning section does
      (Trevor's call: Codex log parsing changes are at least MINOR).*
- [x] 6.3 Run the review on the feature's changes and resolve or defer each finding, recording
      deferrals here.
      *2026-10-15. Code, failure-handling, test, comment and type reviews over `47185a7..HEAD`.
      Fixed, each test-first (red seen before the change unless noted as a guard):*
      - *An unexpected exception in a Codex scan, on the timer thread, ended the app. The monitor
        now records it as a problem beside the last reading.*
      - *Alerts could wait forever if Windows never reported a notification ending. One older
        than a minute is now taken as ended.*
      - *Closing the first-start question with X saved its stand-in at the next HUD drag or tray
        click, so the question never came back. The stand-in is now left out of saves.*
      - *A scan held the lock the UI thread reads every second. Scans now run under their own
        lock; the duplicate startup scan was removed.*
      - *A log moved to archived_sessions mid-scan failed the scan; it is now passed over.*
      - *An ended window said "no use since" while the logs could not be read; now "reset".*
      - *Decided by Trevor, recorded in the spec: a product switched off raises no alerts and
        does not enable Mute, Claude included (new `ProductAlerts`, `UserSettings.Shown`, which
        also replaced three copies of the Claude-alone fallback); a Codex line that is not valid
        JSON is damage and is passed over, so it can no longer block newer readings.*
      - *Comments: two monitor test comments still describing the removed file-time shortcut,
        App's startup comment, and two `CodexRecordResult` comments.*
      - *Tests added as guards for behaviour already right: two windows of one length, a window
        with no reset time, both Codex windows ended, `shown` kept out of the settings file.*
      *Deferred:*
      - *A Codex folder Windows refuses Bingo reads as not installed, since `Directory.Exists`
        returns false for both. Rare in the user's own profile; lifting it means telling absent
        from denied in `CodexLogReader.Scan`.*
      - *A settings file locked or unparseable at startup loads as defaults and is later saved
        over. Present since 0.1 and not Codex work; the first-start question makes it slightly
        likelier. Worth its own task: retry the read, and do not save over a file that exists
        but could not be used.*
      - *`CodexState` and `ProductSwitches` can be built in contradictory states. No caller does;
        `UserSettings.Shown` already covers the all-off case where it is read.*
      *Accepted: the 100 ms pause before a save retry, on the UI thread; comments citing spec
      task numbers, since the specs are committed.*
      *Simplification pass over the fixes: App's constructor moved below its fields, and the
      Claude-alone fallback made one `ProductSwitches.ClaudeOnly`. Full suite 1705 pass, clean
      build 0 warnings/errors. The shell wiring for the alert filter, Mute and the first-start
      stand-in was not checked on screen; the rules it calls are tested in Core.*

## Build Verification

- [x] BV.1 `dotnet clean` then `dotnet build`: 0 warnings, 0 errors.
- [x] BV.2 `dotnet test`: full suite, no failures, no unexplained skips.
      *1705 pass, 0 fail, 0 skip; no warnings in the unfiltered warning lines.*
- [x] BV.3 Both publish shapes build per the README's Publishing section, and the setup builds
      from the self-contained one.
      *Built from `ad67d41` into `artifacts/bv-check/`, apart from the release folders: self-
      contained 70.9 MB, framework-dependent 0.42 MB (0.4.0's were 70.87 and 0.36), setup 65.97 MB.
      Both carry 0.4.0+ad67d41, since the version bump is release step 2. Built, not launched.*
- [~] BV.4 Final checkpoint: all green, every acceptance criterion met (AC-3b deferred), recorded
      in `progress.md`. Release as 0.5.0 by the README's Releasing steps **(Trevor)**.
      *Checkpoint recorded. The shell wiring 6.3 changed (alert filter, Mute, first-start
      stand-in) was then checked on screen with the Debug build, a stub endpoint and a made-up
      Codex record; all four trials passed (details in `progress.md`). Left open: the release
      itself.*
