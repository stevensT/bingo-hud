# Development Constitution

These principles guide all technical decisions in this project. They're defaults, not dogma — any
principle can be overridden for a specific feature by documenting the rationale in that feature's
plan.md. But when no strong reason exists to deviate, follow these.

## Principles

### 1. Test-First
Write tests before implementation code. Tests serve two purposes: they verify correctness, and they
force you to think about the interface before the internals. If you can't write a test for
something, you probably don't understand it well enough to build it.

### 2. Simplicity
Choose the simplest approach that satisfies the requirements. Complexity is a cost — it slows down
future changes, increases the surface area for bugs, and makes onboarding harder. If two solutions
both work, prefer the one that's easier to understand.

### 3. Explicit Over Implicit
Make behavior visible and predictable. Prefer explicit configuration over convention, clear error
messages over silent failures, and named functions over clever one-liners. Code is read far more
often than it's written.

### 4. Integration-First Testing
Prioritize integration tests that exercise real workflows over unit tests that test isolated
functions. Unit tests are fine for complex logic, but integration tests catch the bugs that
actually affect users — the ones that live in the seams between components.

### 5. No Premature Abstraction
Don't abstract until you've seen the pattern repeat. Write concrete code first. When you see the
same pattern three times, then consider extracting it. Abstractions created too early often don't
match the actual shape of the problem, and you end up fighting them.

### 6. Never Display an Unbacked Number
This project reads an undocumented upstream endpoint and renders numbers a person makes decisions
from. Any figure on screen must be traceable to a response we actually received and successfully
parsed, and must carry its age. Never estimate, interpolate, or fall back to a hardcoded plan
limit; a figure the server did not send does not go on screen at any age.

When the source is unavailable, unreadable, or stale, say so. The last figure the server did send
may stay on screen, because it remains the best answer known and hiding it answers nothing — but
its status travels with it and displaces anything that would imply it is still moving. A frozen
percentage beside a running countdown is the failure this principle exists to prevent; the same
percentage labelled with why it is not moving is not.

Amended at task 7.1. The original wording said to show nothing at all when the source was
unavailable, unreadable, or stale, which read on the surface as forbidding the marked stale
reading that AC-8 and AC-13 require. The line was in the wrong place: what makes a readout lie is
not the age of the number but the absence of the label, and a HUD that blanks itself is its own
kind of dishonesty, since it looks identical to a HUD that has crashed.
