# 06.03 — Senior Build Projects

Three projects that change the system's foundations. Each is a multi-week effort with real migration,
test, security, performance, and rollout concerns. These are *not* rewrites — the bones are good; these
add the production dimensions the learning repo deliberately omits. Write an RFC
([07/02 template](../07-career-and-collaboration/02-writing-prs-and-rfcs.md)) before coding.

---

## Project S-1: Durable persistence with EF Core
**Problem:** all state is in-memory ([BillingStore L8-L17](../../src/Billing.Api/Infrastructure/BillingStore.cs#L8-L17));
a restart loses every customer and invoice (R-08).
**Value:** durability, real transactions, queryable reporting — the prerequisite for everything else.
**Design checklist:**
- [ ] Extract `IBillingStore` so the engine depends on the interface, not the class (the seam).
- [ ] Map 11 record types; decide the `Money` mapping (two columns vs owned type) and how `Invoice.Lines`
  becomes a child table.
- [ ] Add the indexes the in-memory version never needed (`(SubscriptionId, InvoiceId, OccurredAt)`,
  unique `Email`/`Code`).
- [ ] Resolve the **captive-dependency** trap: `DbContext` is scoped but the engine + workers are
  singletons ([Program.cs L14-L20](../../src/Billing.Api/Program.cs#L14-L20)) — make the engine scoped or
  open a scope per worker tick.
- [ ] Wrap multi-write ops (`GenerateInvoice`'s 4 writes) in one transaction.
**Files/modules:** new `Billing.Api/Persistence/*`, `Program.cs` DI, every store call site (~20).
**Migration plan:** characterization tests first → `IBillingStore` seam → EF impl behind a config flag →
ship dark → flip per environment.
**Test plan:** the Flow-2 `$1,111.20` total and idempotency tests must stay green across the swap.
**Security plan:** parameterized LINQ only (no raw SQL); connection string from secrets.
**Performance plan:** verify the N+1 in `RunBillingCycle` is gone (batch the usage load); confirm the
new indexes are used (EF query log).
**Rollout plan:** flag-gated; per-env flip; instant revert to in-memory if invoices diverge.
**Open questions:** which provider (SQLite for dev, Postgres for prod)? Owned type vs columns for
`Money`? **Stretch:** add optimistic concurrency (`RowVersion`) to close the lost-update gap.
**This is [Comparison 1](../03-architecture-and-patterns/07-comparative-implementations.md) made real —
do its migration drill first.**

---

## Project S-2: Authentication + multi-tenancy
**Problem:** no auth, no tenant isolation — every endpoint is anonymous over a global dataset; every
id-bearing endpoint is an IDOR (R-02).
**Value:** the difference between a demo and a product. Without it nothing else matters.
**Design checklist:**
- [ ] Add authentication (JWT bearer for API, or cookie for the SPA — pick one and justify).
- [ ] Add `TenantId` to owned entities; identity/tenant comes from validated claims, never the body.
- [ ] Make scoping **non-bypassable**: a tenant-scoped store or a single authorization filter, so a new
  endpoint *can't* forget it.
- [ ] Return 404 (not 403) for not-yours to avoid leaking existence.
- [ ] If you choose cookies, add anti-forgery (CSRF) — currently N/A because there are no cookies.
**Files/modules:** `Program.cs` (auth middleware + DI), every entity (`TenantId`), every store query,
the SPA (auth flow).
**Migration plan:** add `TenantId` nullable → backfill the seed → make non-null → enforce scoping.
**Test plan:** cross-tenant access returns 404 (the currently-impossible test in
[05/02 Recipe 4](../05-quality-engineering/02-writing-tests-here.md)); every store query is scoped.
**Security plan:** this *is* the security plan — pair with [05/05](../05-quality-engineering/05-security-checklist.md).
**Performance plan:** the `TenantId` filter must be indexed.
**Rollout plan:** this is hard to flag (it changes the trust model); stage it on a non-prod environment
behind the new auth before exposing.
**Open questions:** one tenant per customer, or tenants owning many customers? **Stretch:** role-based
authorization (operator vs read-only).

---

## Project S-3: Real, reliable webhook delivery
**Problem:** outbound delivery is simulated; it always "succeeds", the retry/backoff branch is dead, and
the enqueue isn't transactional with the business write (R-06).
**Value:** at-least-once delivery to customer endpoints — a feature real billing systems sell.
**Design checklist:**
- [ ] Introduce `IWebhookSender` (real `HttpClient` via `IHttpClientFactory` with a timeout) so failures
  are *reachable and testable*.
- [ ] Make the retry/backoff path live ([worker L41-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L41-L53))
  and add a dead-letter after N attempts.
- [ ] Write the outbox row in the *same transaction* as the business change (depends on S-1).
- [ ] Add receiver-side idempotency keys + signing so *your* customers can dedup (mirror the inbound
  verifier).
- [ ] Validate destination URLs (SSRF — `WebhookEvent.Destination` is data).
**Files/modules:** `WebhookDeliveryWorker`, a new `IWebhookSender`, `Program.cs` (HTTP client + Polly),
the engine's enqueue sites.
**Migration plan:** add the sender behind the existing simulated behavior; flip to real sending per
environment.
**Test plan:** force the sender to throw → assert backoff + requeue + eventual dead-letter; this is the
test that's *impossible today* because nothing can fail.
**Security plan:** allowlist destinations; sign outbound payloads; never deliver to internal addresses.
**Performance plan:** bounded concurrency for delivery; don't drain the whole queue on one thread under
real I/O.
**Rollout plan:** simulated → real, flag-gated; watch the new failure counters.
**Open questions:** Polly vs hand-rolled backoff? Per-customer delivery rate limits? **Stretch:**
ordered delivery per subscription.

## How to run a senior project

1. Write the RFC (template in [07/02](../07-career-and-collaboration/02-writing-prs-and-rfcs.md)) —
   motivation anchored, alternatives, migration + rollback, open questions.
2. Get the design reviewed (self-review against the critique's "if I owned this 3 months" sequencing —
   note S-1 should usually precede S-3 because the transactional outbox depends on a real DB).
3. Build behind a flag; ship dark; flip per environment; keep the revert path.

## Verification Notes

Every problem statement maps to a confirmed risk-register entry, anchored on 2026-06-09. The captive-
dependency and transaction points (S-1) are read-verified from the current singleton registrations.
Nothing was executed; the projects are designs, not implementations.
