> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/05-quality-engineering/03-systematic-debugging.md.txt).

# 05.03 — Systematic Debugging

Debugging is a method, not a talent. The loop, every time:

1. **Reproduce** — get a reliable trigger. No repro, no fix.
2. **Narrow** — binary-search the system; which side of a boundary is it on?
3. **Hypothesize** — one falsifiable guess at a time.
4. **Test cheaply** — the smallest probe that confirms or kills the hypothesis.
5. **Fix the root cause** — not the symptom.
6. **Add regression coverage** — a test that fails before, passes after.

Probes available here: the .NET debugger / breakpoints, `ILogger` output, the browser **network
panel**, and (once you add it) EF query logging. There's no profiler story yet.

Four scenarios grounded in this repo's real risks.

---

## Scenario: The dashboard returns 500 in production but works locally

**Reproduction:** create a plan with currency `"EUR"`, a customer, an active subscription on it; then
`GET /api/dashboard`.

**First question:** is it the *data* or the *code*? It works on the all-USD seed and fails with EUR —
so it's data-dependent code, not infra.

**Narrowing path:**
1. The dashboard calls many store reads + `CalculateMetrics`. Bisect: does `GET /api/customers`
   (no metrics) still work? If yes, the fault is in metric computation, not storage.
2. Inside [`CalculateMetrics`](../../src/Billing.Api/Program.cs#L254-L290), the only money *addition*
   across entities is the MRR/outstanding aggregation ([L262-L278](../../src/Billing.Api/Program.cs#L262-L278)).
3. The accumulator is seeded `Money.Zero("USD")` ([L262](../../src/Billing.Api/Program.cs#L262)); the
   added value is the EUR plan price.

**Useful probes:** a breakpoint at [Money.EnsureSameCurrency L65](../../src/Billing.Api/Domain/Money.cs#L65);
or just read the exception message — it's literally `"Currency mismatch: USD vs EUR."`.

**Likely root cause:** R-01 — currency coupling; `Money.Add` throws and (no `UseExceptionHandler`) it
surfaces as a 500.

**Regression test to add:** integration test — EUR plan + active sub → `GET /api/dashboard` returns
200 (red today). Then fix with per-currency metrics or a reporting currency.

**Senior lesson:** "works locally" almost always means "my data is narrower than production's". The
seed being all-USD *hid* a whole class of bug. Distrust demos that only exercise one value of a
first-class field.

---

## Scenario: A customer was charged twice

**Reproduction:** apply a payment to an invoice (UI "Pay"), then POST the *same* signed provider
webhook for that invoice twice within 5 minutes.

**First question:** which path created the duplicate — the worker, or the inbound webhook? Both call
the same engine method.

**Narrowing path:**
1. List the `Payment` rows for the invoice (the dashboard shows them). How many `Succeeded`? More than
   one ⇒ duplicate application.
2. Both [`PaymentWorker.Process`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L40-L44) and the
   [webhook endpoint](../../src/Billing.Api/Program.cs#L243) call
   [`ApplyPaymentResult`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200).
3. `ApplyPaymentResult` writes a **new** `Payment` every call ([L170-L179](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L170-L179))
   with no dedup; the inbound webhook never checks `EventId`.

**Useful probes:** log/inspect the count of payments per invoice before/after the second webhook;
breakpoint at [ApplyPaymentResult L170](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L170).

**Likely root cause:** R-05 — `ApplyPaymentResult` is not idempotent; the 5-min replay window + missing
`EventId` dedup lets a retried webhook re-apply.

**Regression test to add:** call `ApplyPaymentResult` twice → assert exactly one `Succeeded` payment;
POST the same webhook twice → assert one effect. Fix: dedup on `EventId`/`providerReference` at the
engine boundary.

**Senior lesson:** the invoice-status guard ([worker L35-L38](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L35-L38))
mutes the *effect* but not the *record* — "looks paid" hid "charged twice". Idempotency must protect
the side effect, not just the final state.

---

## Scenario: Recorded usage never shows up on the invoice

**Reproduction:** record a usage event, run the cycle, find the usage isn't billed.

**First question:** was the usage *stored*, or *stored-but-not-selected*?

**Narrowing path:**
1. `GET /api/dashboard` → is the usage event present with `invoiceId: null`? If present, it was stored
   — the bug is in *selection*, not recording.
2. [`GetUnbilledUsage`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L182-L188) selects on
   `InvoiceId == null && OccurredAt ∈ [CurrentPeriodStart, CurrentPeriodEnd)`.
3. Check the event's `OccurredAt` vs the subscription's current period. The UI sends
   `occurredAt: new Date().toISOString()` ([App.tsx L141](../../src/Billing.Web/src/App.tsx#L141)) — if
   "now" is *after* `CurrentPeriodEnd`, it's excluded.

**Useful probes:** compare `usageEvent.occurredAt` to `subscription.currentPeriodEnd` in the dashboard
JSON. No debugger needed — it's a data comparison.

**Likely root cause:** the half-open period window ([store L185-L186](../../src/Billing.Api/Infrastructure/BillingStore.cs#L185-L186))
— usage dated outside the *current* period is deferred, not billed now. Often *correct behavior*
mistaken for a bug; sometimes a real off-by-one at the boundary.

**Regression test to add:** record usage at `CurrentPeriodEnd - 1s` (billed) and `CurrentPeriodEnd + 1s`
(deferred); assert which period each lands in.

**Senior lesson:** "missing" data is frequently "filtered out by a predicate you didn't read". Always
confirm *stored vs selected* before suspecting a write bug.

---

## Scenario: The dashboard won't load at all (frontend/network)

**Reproduction:** run only the API on a non-default port, or run the SPA on a port other than 5173;
the dashboard shows "Load failed".

**First question:** did the request leave the browser, reach the server, and come back? This is a
boundary-isolation problem — use the **network panel**.

**Narrowing path:**
1. Network panel: is there a request to `:5088/api/dashboard`? If it's red with a **CORS** error, the
   server reached but rejected the origin.
2. CORS allows only `http://localhost:5173` / `127.0.0.1:5173`
   ([Program.cs L26](../../src/Billing.Api/Program.cs#L26)). If Vite fell back to a different port
   (`strictPort: false`, [vite.config L8](../../src/Billing.Web/vite.config.ts#L8)), the origin won't match.
3. Or: is `VITE_API_BASE_URL` pointing at the wrong API port? ([api.ts L1](../../src/Billing.Web/src/api.ts#L1)).

**Useful probes:** the network panel's request/response headers; the console error text (CORS vs
connection-refused are different failures).

**Likely root cause:** origin/port mismatch — either the SPA port drifted (breaking CORS) or the API
base URL is wrong.

**Regression test to add:** not a unit test — a documented run check (correct ports) plus, if you want
robustness, make the CORS origins configurable instead of hardcoded.

**Senior lesson:** when the UI "doesn't work", isolate the boundary first (browser → network → server),
don't read component code. A CORS rejection looks like a frontend bug and is actually a server config.

## The debugging checklist (tape to your monitor)

- [ ] Can I reproduce it on demand?
- [ ] Which side of which boundary is it on? (UI / network / endpoint / engine / store)
- [ ] What's my *one* falsifiable hypothesis?
- [ ] What's the *cheapest* probe to test it? (Often: read the data, not the code.)
- [ ] Am I fixing the root cause or a symptom?
- [ ] Did I add a test that would have caught this?

## Verification Notes

Every narrowing path is read-verified against the anchored code on 2026-06-09. The "surfaces as 500"
step in scenario 1 is the same hypothesis flagged in the [critique](../03-architecture-and-patterns/06-architecture-critique.md)
(no global handler found, not executed). Nothing here was run.
