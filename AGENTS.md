# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

This file is gitignored and stays that way. Anything a human contributor would need — what Bingo
is, architecture, build commands, versioning policy, repo layout — belongs in `README.md`, which
is committed. Read `README.md` first; it is the source of truth for all of that, and this file
must not duplicate it. What lives here is only the working agreement between Trevor and Codex.

## Environment and skill level

- I am new to software development. Explain your reasoning and flag concepts I should learn.
  Don't assume I can spot a bad pattern.
- Windows 11, PowerShell, .NET 9 SDK, WPF, VS Code with C# Dev Kit. No Visual Studio.
- All commands must be PowerShell-compatible. No bash-isms, no `~` for home directory.
  The one exception: a command Trevor runs with the `!` prefix in Codex executes in Git
  Bash, so it must use bash syntax (for example `! "/c/Users/Trevor/…/setup.exe"`).
- Reference file paths and line numbers in explanations so I can jump to them.
- Before running any command that installs, deletes, or modifies files outside the project
  folder, ask me first.
- Prefer boring, explicit code over clever code.
- The OAuth token in `~/.Codex/.credentials.json` cannot be read directly — the tool classifier
  blocks it, correctly. Capture endpoint responses with `node scripts/capture-usage.js`, which
  uses the token in-process and never prints it. Do not work around the block.

## Challenge before building

Trevor wants premises tested, not just executed. When a spec, plan, or request rests on an
assumption that has not been checked, say so before building on it — name the riskiest
assumption, what it costs if it is wrong, and the cheapest way to find out.

This is not licence to refuse work or to relitigate settled decisions. Raise the concern once,
concisely, then deliver the full task under stated assumptions.

## Planning vs. execution

Spec-Driven Development (the `spec-driven-development:sdd` skill) is the single source of truth for
planning. Specs, plans, and task lists live in the SDD directory structure under `specs/`.
Never create ad-hoc plan or task files anywhere else.

Superpowers skills are execution-only — TDD loop, systematic debugging, refactoring — and are
always scoped to the one SDD task currently in progress. Do not invoke Superpowers brainstorming
or planning skills; SDD owns that phase. This includes `writing-plans`: implementation plans come
from SDD Phase 3/4, not from Superpowers.

### Spikes

A spike is throwaway code written to answer one question, not to ship. It is the one approved
exception to the rule above about ad-hoc files.

- Record it as `specs/[feature]/spikes/[name].md`: the question, the method, what it does *not*
  test, and the decision criteria — written before the experiment runs, so the result cannot be
  rationalised afterwards.
- Test-first may be suspended for a spike, because the output is a decision rather than a
  component. State the suspension and its reasoning in the spike document. Constitution
  Principle 6 is never suspended.
- A spike is committed so the experiment is reproducible, and deleted when it closes, with the
  result recorded in the same document.

## Design decisions

When a design argument is genuinely two-sided, propose a setting rather than picking a winner —
and say what the setting costs. Configurability is never free: a value the user can flip is a
value the display has to label.

## Review gate

Run `/pr-review-toolkit:review-pr` only when an SDD task is code-complete and its tests pass.
Not mid-implementation.

An SDD task is not done until review findings are either resolved or explicitly deferred with a
note recorded in the task file.

## Git

Trevor owns git. He stages, commits, and pushes — in deliberate chunks. Codex never runs
`git commit` or `git push`.

Codex's job is to say when a chunk looks like a good commit boundary and propose the message.
Changes to `progress.md` and `tasks.md` alone are not a commit boundary; they go in with the next
commit that changes the project. Message rules:

- Short subject line, past tense. A record of what this commit did — "Added the credential
  reader" — not an instruction and not a description of what the software does. The verb names
  the edit to the repository: added, completed, expanded, settled. Verbs like read, parse, or
  show describe the running program and belong in the body.
- Body as bullets, not prose, and past tense throughout to match the subject.
- Concise, but each bullet says clearly what changed.
- No hard wrapping. Each bullet is one line however long it runs; wrapping is the reader's job.
- Written the way a person writes it — no marketing tone, no filler, no emoji, no AI attribution
  trailers.

This repository is going public. Nothing public-facing should name the tooling — not commit
messages, committed docs, filenames, source comments, or issue text. Describe what changed in the
code and docs, not which skill, agent, or workflow produced the change, and make no reference to
this file, to skills, or to slash commands. Commit messages deserve the most care here because
they are the most visible thing in a repository.

Where a tool's own convention would put its name in the code — a marker comment for a deliberate
shortcut, say — use a neutral word instead. A shortcut with a known ceiling is marked
`// deferred:` followed by what the ceiling is and what would lift it. The marker earns its place
by being greppable, not by being branded.

That is a presentation choice rather than concealment — the work is what gets described, and the
tooling is not part of the work. References to Codex are fine where they describe the
product Bingo monitors; that is subject matter, not provenance.

## Development Methodology — Spec-Driven Development (SDD)

All feature work follows the spec → plan → tasks → execute pipeline.

### Build & Test Commands

See the Building section of `README.md`. It is the single source of truth; do not duplicate
commands here.

### Before implementing any feature

1. Read `specs/[feature]/spec.md` — what to build and why
2. Read `specs/[feature]/plan.md` — the technical approach
3. Read `specs/[feature]/tasks.md` — the current task
4. Read `specs/memory/constitution.md` — architectural principles

### While implementing

- Follow the task list in order. Don't skip ahead.
- Write the test FIRST. Run it, confirm it fails, then implement until it passes. Non-negotiable.
- Mark tasks `[~]` when starting and `[x]` when done. Keep tasks.md in sync at all times.
- `[P]` tasks are launched as concurrent subagents via the Agent tool when the group is large
  enough to be worth it, or when the units are genuinely independent pieces of work. A small
  group of pure functions may be run inline and in sequence instead — that was done for 4.1 to
  4.3 and the units turned out to inform each other. Say which way it was done, and why, in the
  checkpoint record.
- At `[C]` checkpoints: run `dotnet clean` first, then the full test suite AND the build, audit
  the task marks, check against the spec's acceptance criteria, and record the result in
  progress.md before continuing. The clean is not optional. An incremental build does not
  re-report warnings for projects it did not rebuild, so a warm build claiming zero warnings
  proves nothing — the 6.12 checkpoint found five that every previous build had hidden.

### Non-goals are hard boundaries

Do not implement anything listed under Non-Goals in a feature's spec.md, even if it looks like a
small addition.

### When starting a new session

1. Read `specs/[feature]/progress.md` for where the last session stopped
2. Run the full test suite and build to confirm reality matches the last checkpoint
3. Pick up from the first incomplete task in tasks.md
