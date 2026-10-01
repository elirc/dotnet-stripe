> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/06-contribution-practice/02-mid-level-feature-tickets.md.txt).

# 06.02 — Mid-Level Feature Tickets

Cross-layer changes: they touch the engine and/or the API and/or the UI, need a design decision, and
must ship with tests, a risk note, and a rollback. Five tickets, each tied to a real risk.

---

## Ticket M-1: Catch-up billing for missed periods
**Difficulty:** Medium · **Time:** half a day · **Skills:** the billing engine, period math, tests
**Story:** As the business, I want a subscription that's several periods overdue to be billed for *all*
elapsed periods, so we don't silently lose revenue. (R-07)
**Why it's a good contribution:** fixes confirmed revenue loss; localized to the engine; highly
testable with the injected clock.
**Acceptance criteria:**
- [ ] Running the cycle for a subscription 3 periods overdue produces 3 invoices (or one invoice
  covering 3 periods — decide and document which).
- [ ] Each invoice has the correct period window and the period advances correctly.
- [ ] Tests cover 0, 1, and 3 overdue periods.
**Read first:** [`RunBillingCycle` L115-L127](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127)
(one invoice per run) and the single-step period advance [L84-L88](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L84-L88).
**Design notes:** the cleanest fix loops `while (sub.CurrentPeriodEnd <= billingDate)` generating an
invoice and re-reading the advanced subscription each iteration. Decide the **usage-attribution** rule:
each period's invoice should only pull usage in *that* period's window (the existing
[`GetUnbilledUsage`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L178-L190) already filters by
the subscription's *current* window, which advances each loop — verify that interaction).
**Risk & rollback:** could over-bill if the loop condition is wrong — pin behavior with tests first.
Rollback: feature-flag the loop vs the single-step path; flip back if invoice counts look wrong.

---

## Ticket M-2: Make payment application idempotent
**Difficulty:** Medium · **Time:** half a day · **Skills:** idempotency, the engine + store
**Story:** As the business, I want a retried provider webhook (or a re-run job) to apply a payment
*once*, so customers aren't double-charged and accounting isn't corrupted. (R-05)
**Why it's good:** closes a money-correctness hole that *will* trigger (providers retry).
**Acceptance criteria:**
- [ ] Calling `ApplyPaymentResult` twice for the same logical event yields exactly one `Succeeded`
  payment and one outbound webhook.
- [ ] The inbound webhook dedups on `EventId`.
- [ ] Tests: apply twice → one effect; replay the same signed webhook → one effect.
**Read first:** [`ApplyPaymentResult` L157-L200](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200)
(new payment each call), the unused `EventId` at [webhook endpoint L243-L244](../../src/Billing.Api/Program.cs#L243-L244).
**Design notes:** add a processed-events set in the store (keyed by `EventId` or
`(invoiceId, providerReference)`); check-and-record atomically under the store lock. Decide where the
dedup lives — engine boundary (covers both the worker and the webhook) is best.
**Risk & rollback:** a too-narrow key lets duplicates through; a too-broad key drops legitimate retries
of *different* events. Rollback: the dedup is additive; remove the check to revert.

---

## Ticket M-3: Cancel a subscription
**Difficulty:** Medium · **Time:** half a day · **Skills:** API + engine + UI + state
**Story:** As an operator, I want to cancel a subscription so it stops being invoiced.
**Why it's good:** exercises a real state transition (`Active` → `Canceled`), a status that's defined
but never set ([enum L21](../../src/Billing.Api/Domain/BillingEnums.cs#L21)), across all layers.
**Acceptance criteria:**
- [ ] `POST /api/subscriptions/{id}/cancel` sets status `Canceled`.
- [ ] `RunBillingCycle` skips canceled subs (it already filters Active/Trialing,
  [L118](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L118)) — verify, and confirm
  `GenerateInvoice` rejects a canceled sub ([L16-L19](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L16-L19)).
- [ ] The UI shows a Cancel action and reflects the new status.
- [ ] Tests at each layer.
**Read first:** the subscriptions table render [App.tsx L287-L308](../../src/Billing.Web/src/App.tsx#L287-L308),
`SaveSubscription` [store L162-L165](../../src/Billing.Api/Infrastructure/BillingStore.cs#L162-L165).
**Design notes:** decide the semantics — cancel-at-period-end vs immediate. Idempotent (cancel-twice is
a no-op). Should it emit a `subscription.canceled` webhook for consistency with the others?
**Risk & rollback:** mid-cycle cancellation + proration is out of scope — say so explicitly. Rollback:
additive endpoint + UI button; remove to revert.

---

## Ticket M-4: Per-currency dashboard metrics
**Difficulty:** Medium · **Time:** half a day · **Skills:** the metrics layer, money, DTO change
**Story:** As an operator with multi-currency customers, I want MRR/outstanding broken out by currency,
so the dashboard doesn't crash and the numbers make sense. (R-01)
**Why it's good:** removes the highest-ranked latent bug's trigger without needing an FX layer.
**Acceptance criteria:**
- [ ] `CalculateMetrics` no longer seeds a single `Money.Zero("USD")`; it groups by currency.
- [ ] `GET /api/dashboard` returns 200 with a EUR plan + active subscription present (the R-01 test goes green).
- [ ] The `DashboardMetrics` DTO + the React type + the UI render the per-currency breakdown.
- [ ] Tests: mixed-currency dataset returns 200 with correct per-currency sums.
**Read first:** [`CalculateMetrics` L254-L290](../../src/Billing.Api/Program.cs#L254-L290),
[`DashboardMetrics` DTO L22-L28](../../src/Billing.Api/Contracts/Responses.cs#L22-L28),
[the metrics UI L181-L187](../../src/Billing.Web/src/App.tsx#L181-L187).
**Design notes:** this is a **public-contract change** (`MonthlyRecurringRevenue` becomes a list/map).
Coordinate the DTO + TS change. Consider whether to *also* offer a single reporting-currency total
(needs FX — out of scope; note it).
**Risk & rollback:** breaking the metrics DTO breaks the UI if not coordinated. Rollback: keep the old
USD-only field alongside the new map for one release, then remove.

---

## Ticket M-5: Configurable CORS origins
**Difficulty:** Medium · **Time:** 2–3 h · **Skills:** options pattern, config, the pipeline
**Story:** As an operator deploying to staging/prod, I want CORS origins from config, so the SPA isn't
hardcoded to `localhost:5173`.
**Why it's good:** removes a deployment foot-gun (the [hardcoded origins L26](../../src/Billing.Api/Program.cs#L26)
+ Vite's `strictPort: false` cause the [CORS failure in 05/03](../05-quality-engineering/03-systematic-debugging.md));
teaches the options pattern end to end.
**Acceptance criteria:**
- [ ] CORS origins read from `BillingOptions` (or a new `Cors` section) with the current dev values as defaults.
- [ ] Misconfiguration is caught at startup (empty origins → fail or warn).
- [ ] A test or documented manual check.
**Read first:** the CORS policy [L21-L30](../../src/Billing.Api/Program.cs#L21-L30) and
[`BillingOptions` L3-L9](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L3-L9).
**Design notes:** follow the existing options-binding pattern; add `ValidateOnStart`. **Don't** widen
to `AllowAnyOrigin` — that's a security regression.
**Risk & rollback:** misconfigured origins break the SPA; default to today's values so an empty config
behaves as now. Rollback: revert to the constant list.

## Verification Notes

Every anchor read-verified on 2026-06-09. Each ticket maps to a confirmed risk-register entry; the
"public-contract change" flags (M-3 webhook, M-4 DTO) are read-verified consequences of the current
shapes. No code was executed.
