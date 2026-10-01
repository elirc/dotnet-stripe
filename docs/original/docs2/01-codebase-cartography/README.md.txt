# 01 — Codebase Cartography

Before you can change a system safely, you need a map: what the major parts are, who owns what, the
order to read files in, the vocabulary, the tooling, and how a request actually flows. This module
builds that map for the Subscription Billing Dashboard.

| File | What it gives you |
| --- | --- |
| [01-system-map.md](01-system-map.md) | The project graph and ownership: UI / API / domain / data / workers, public vs private surface. |
| [02-file-reading-order.md](02-file-reading-order.md) | 20+ files in a deliberate order, split junior / mid / senior, each with "what to look for". |
| [03-domain-glossary.md](03-domain-glossary.md) | The billing nouns (invoice, coupon, MRR, idempotency key…) and where each lives in code. |
| [04-runtime-and-tooling-map.md](04-runtime-and-tooling-map.md) | SDK / MSBuild / NuGet / npm / Vite, the browser↔server↔worker boundaries, env & secrets. |
| [05-key-flows.md](05-key-flows.md) | 4 end-to-end flows with trace tables — the heart of orientation. |

**Start here:** read [01-system-map.md](01-system-map.md), then trace one flow in
[05-key-flows.md](05-key-flows.md). The rest is reference you'll return to.

**Orienting fact that explains the whole repo:** there is exactly one piece of state — the in-memory
[BillingStore](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5) — and almost every design
decision (idempotency, workers, the fat dashboard payload) is shaped by the fact that this store is
*not* a real database yet. Hold that thought; the [comparison cards](../03-architecture-and-patterns/07-comparative-implementations.md)
pay it off.
