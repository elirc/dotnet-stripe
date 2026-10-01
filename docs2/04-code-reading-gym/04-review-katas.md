> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/04-code-reading-gym/04-review-katas.md.txt).

# 04.04 — Review Katas

Five fake PRs against this repo, each with planted issues. Review each *before* reading "Expected
findings". Write findings with a **severity label** (Blocking / Important / Optional) and an **anchor**
to the pattern it resembles, per [07/01](../07-career-and-collaboration/01-code-review-mindset.md).

---

## Kata 1: "Add EUR pricing for European customers"

**Author intent:** let customers be billed in EUR.
**Fake diff summary:** adds a EUR plan via the existing `POST /api/plans` (currency `"EUR"`), creates a
subscription, no other changes.
**Resembles:** [CalculateMetrics L262-L274](../../src/Billing.Api/Program.cs#L262-L274).

**Expected findings:**
- **Blocking:** the dashboard will 500. MRR sums into `Money.Zero("USD")` and adds the EUR plan price →
  `Money.Add` throws ([Money L65](../../src/Billing.Api/Domain/Money.cs#L65)) (R-01). The PR "works" in
  isolation but breaks every dashboard load.
- **Important:** `GenerateInvoice` will also throw for a EUR customer on this plan
  ([engine L34, L62](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L34-L62)).
- **Optional:** add a test that creates a EUR plan and asserts the dashboard returns 200.

**Good comment example:**
> **Blocking.** This is a great feature but it'll take the dashboard down: metrics aggregate in USD
> ([L262](../../src/Billing.Api/Program.cs#L262)) and `Money.Add` throws on a currency mismatch. Can we
> add per-currency MRR or a reporting currency first? I'll pair if useful — this is the repo's #1
> latent bug (R-01), not your fault.

---

## Kata 2: "Cache the dashboard metrics for speed"

**Author intent:** stop recomputing metrics on every request.
**Fake diff summary:** stores `CalculateMetrics(...)` result in a `static DashboardMetrics? _cache`,
returns it if non-null; never invalidates.

**Expected findings:**
- **Blocking:** stale data forever — creating a customer/invoice/payment won't update the cached
  metrics. The whole app's premise is "refetch after write" ([App.tsx runAction L73-L84](../../src/Billing.Web/src/App.tsx#L73-L84)); a never-invalidated cache breaks it.
- **Important:** a `static` field is process-global shared mutable state without the store's lock
  discipline ([BillingStore L7](../../src/Billing.Api/Infrastructure/BillingStore.cs#L7)) — a thread-safety regression.
- **Optional:** if caching is really needed, key it on a version/stamp that bumps on any write, or use
  a short TTL like the [idempotency cache](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L104-L110).

**Good comment example:**
> **Blocking.** Metrics are derived from mutable state, so a permanent cache with no invalidation will
> show stale MRR after any write. Given the app already refetches the whole dashboard after each
> action, what's the measured win here? If we do cache, let's key it on a write-version stamp.

---

## Kata 3: "Bulk usage import endpoint"

**Author intent:** accept an array of usage events in one POST.
**Fake diff summary:** `POST /api/usage-events/bulk` loops the array calling `store.AddUsageEvent` per
item; no quantity bound; no `Idempotency-Key` handling beyond the default middleware.

**Expected findings:**
- **Important:** no upper bound on `Quantity` per item ([existing check only `> 0`, L180](../../src/Billing.Api/Program.cs#L180)) — a 2-billion-unit row produces an absurd invoice (R-12).
- **Important:** partial failure — if item 7 of 10 throws (`EnsureExists`), items 1-6 are already
  saved and there's no rollback (no transaction; R-08). Define the contract: all-or-nothing, or report
  per-item results.
- **Blocking (if it returns 200 on partial failure):** swallowing per-item errors into an overall
  success hides data loss (Contrast 4).
- **Optional:** the per-item `AddUsageEvent` takes the lock N times; one batched locked call would be
  cleaner.

---

## Kata 4: "Refund an invoice"

**Author intent:** `POST /api/invoices/{id}/refund`.
**Fake diff summary:** sets invoice → `Void`, writes a negative `Payment`, enqueues a webhook; wraps the
whole thing in `try { ... } catch { return Results.Ok(); }`.

**Expected findings:**
- **Blocking:** the bare `catch` returns 200 on *any* failure, hiding errors as success (Contrast 4 —
  the repo's real endpoints use typed `catch … when` filters, e.g. [L170-L173](../../src/Billing.Api/Program.cs#L170-L173)).
- **Blocking:** not idempotent — a retried refund writes a second negative payment (R-05 shape). Refunds
  must dedup.
- **Important:** no authorization — any caller refunds any invoice (R-02).
- **Important:** `Void` is one of the never-used statuses ([enum L28](../../src/Billing.Api/Domain/BillingEnums.cs#L28)) — confirm the intended state machine (refunded vs void are different).
- **Optional:** a negative `Money` is allowed by the type — is that the intended representation, or a
  separate `Refund` entity?

---

## Kata 5: "Switch Money to decimal to simplify"

**Author intent:** "minor units are confusing; use `decimal` dollars."
**Fake diff summary:** replaces `Money` with `decimal` throughout.

**Expected findings:**
- **Blocking:** reintroduces floating-ish accumulation error and loses the currency guard — the exact
  thing `Money` exists to prevent ([Money rationale](../../src/Billing.Api/Domain/Money.cs#L5-L68),
  [README L8](../../README.md#L8)). Summing many invoice lines drifts.
- **Blocking:** loses currency-mismatch detection — EUR and USD `decimal`s add silently (which would
  *mask* R-01 by producing wrong numbers instead of an error — arguably worse).
- **Important:** breaks the wire contract (`MoneyDto` → `decimal`) and the entire React `MoneyValue`
  type ([api.ts L37-L41](../../src/Billing.Web/src/api.ts#L37-L41)).

**Good comment example:**
> **Blocking, with respect.** `Money`'s integer minor units are the best decision in this repo — they
> make totals deterministic and catch currency mixups. `decimal` brings back rounding drift and
> silently adds across currencies. If the goal is readability, can we instead add doc comments / a
> `ToString` helper? Let's not trade correctness for brevity here.

---

## How to score yourself

For each kata: did you find the **Blocking** issue, **label** severity, **anchor** to the resembling
pattern, and write a comment that's **specific + kind**? **Basic:** found the headline issue.
**Solid:** labeled + anchored all findings. **Strong:** your comment teaches the author, offers help,
and notes what the PR did well.

## Verification Notes

Katas are illustrative PRs; every anchor they reference (Money, CalculateMetrics, the catch-when
filters, the enums, api.ts) is read-verified against the real files on 2026-06-09. The planted issues
mirror real risk-register entries so the practice transfers to actual review.
