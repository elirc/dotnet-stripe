# 03 — Architecture & Patterns

This is where you stop describing the code and start *judging* it. Boundaries, persistence, auth,
reliability — then a catalog of the patterns worth stealing, an honest critique, and the comparison
cards that train the core senior skill: choosing between viable designs and knowing what each
commits you to.

| File | What it builds |
| --- | --- |
| [01-boundaries-and-layers.md](01-boundaries-and-layers.md) | What each layer owns and must not own; the leaks. |
| [02-data-model-and-persistence.md](02-data-model-and-persistence.md) | Entities, relationships, the (missing) indexes, "transaction" boundaries, safe schema change. |
| [03-validation-auth-and-permissions.md](03-validation-auth-and-permissions.md) | The validation layers, and the gaping absence of authn/authz/tenant isolation. |
| [04-side-effects-async-and-reliability.md](04-side-effects-async-and-reliability.md) | Queues, workers, webhooks; idempotency, retries, outbox, timeouts, failure visibility. |
| [05-pattern-catalog.md](05-pattern-catalog.md) | 9 pattern cards — the transferable shapes. |
| [06-architecture-critique.md](06-architecture-critique.md) | Strongest choices, real risks, "if I owned this for 3 months". |
| [07-comparative-implementations.md](07-comparative-implementations.md) | 3 comparison cards incl. a migration drill — **the senior skill**. |

**Recommended order:** 01 → 02 → 03 → 04 (understand the structure), then 05 (name the patterns),
then 06 + 07 (judge them). Because the repo is **synthetic**, 06 and 07 pull no punches: clean-looking
generated code is exactly where missing tenant checks, currency coupling, and idempotency races hide.
