# 05 — Quality Engineering

The skills that keep a system correct over time: testing, debugging, performance, security, and
operability. This repo is a stark teaching case because it has **zero tests** (R-03) — so module 05 is
partly "here's the strategy" and partly "here's the first test you'll ever add to this codebase".

| File | What it builds |
| --- | --- |
| [01-testing-strategy.md](01-testing-strategy.md) | What belongs in unit / integration / E2E here; what *not* to test; flake prevention. |
| [02-writing-tests-here.md](02-writing-tests-here.md) | Concrete recipes (with runnable illustrative code) for the first tests. |
| [03-systematic-debugging.md](03-systematic-debugging.md) | The debugging loop + 4 repo-specific scenarios. |
| [04-performance-thinking.md](04-performance-thinking.md) | Measure first; the real hotspots here; N+1, serial awaits, unbounded queries. |
| [05-security-checklist.md](05-security-checklist.md) | Authz, IDOR, injection, secrets, webhooks — tied to real code; a pre-merge checklist. |
| [06-observability-and-operations.md](06-observability-and-operations.md) | "How would I know this broke?" per flow. |

**The honest framing:** because nothing is tested, every behavior in this suite is read-verified, not
test-verified. Your highest-leverage contribution to this repo is the first test file. Start with
[02](02-writing-tests-here.md), recipe 1.
