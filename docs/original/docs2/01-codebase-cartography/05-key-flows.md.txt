# 01.05 — Key Flows

Four end-to-end traces. Each follows one request from the edge to storage and back. Do at least
**Flow 2** by hand — its invoice total is fully worked out from the seed data, so you can check your
understanding against a real number without running anything.

All traces are **hand-traced from source** on 2026-06-09; the .NET runtime was unavailable, so none
were executed. Anchors are read-verified.

---

## Flow 1: Create a customer (write + idempotency + validation)

**Why it matters:** it's the simplest mutation, and it exercises the full write spine —
CORS → idempotency middleware → validation → store uniqueness → DTO back. Every other POST is a
variation on this.

**Open first:** [`POST /api/customers`](../../src/Billing.Api/Program.cs#L67-L83) — the endpoint; then
[`AddCustomer`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76).

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Browser | [App.tsx createCustomer](../../src/Billing.Web/src/App.tsx#L86-L92) | Form submit → `postJson("/api/customers", {name,email,currency})` | `CustomerForm` | none |
| 2 | Client | [api.ts postJson](../../src/Billing.Web/src/api.ts#L149-L158) | Adds `Content-Type` + a fresh `Idempotency-Key` | JSON body + headers | key is per-call, so a retry of the *same* click generates a *new* key |
| 3 | Server | [CORS](../../src/Billing.Api/Program.cs#L34) | Origin `:5173` allowed | — | other origins blocked by browser |
| 4 | Server | [IdempotencyMiddleware](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9-L49) | POST+key → hash body, check cache. First time: miss → buffer response | cacheKey `POST:/api/customers:<key>` | **no key ⇒ no protection** ([L17-L21](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L17-L21)) |
| 5 | Endpoint | [Program.cs L69-L72](../../src/Billing.Api/Program.cs#L69-L72) | Reject if name/email blank → `400 {error}` | — | only presence checked; email format not validated |
| 6 | Endpoint | [Program.cs L76](../../src/Billing.Api/Program.cs#L76) | Build `Customer` (trim, normalize currency, `clock.UtcNow`) | `Customer` record | currency normalize ≠ validate ("ZZZ" passes) |
| 7 | Store | [AddCustomer L68-L73](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68-L73) | Under lock: reject duplicate email (`InvalidOperationException`), else add | dict insert | uniqueness is a **linear scan** |
| 8 | Endpoint | [Program.cs L77-L82](../../src/Billing.Api/Program.cs#L77-L82) | `201 Created` with the customer, or map exception → `400` | `Customer` JSON | — |
| 9 | Middleware | [L63-L67](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L63-L67) | 2xx → cache the response under the key (TTL 24h) | cache entry | non-2xx is **not** cached, so failures aren't replayed |
| 10 | Browser | [runAction](../../src/Billing.Web/src/App.tsx#L73-L84) | On success → `refresh()` re-pulls the whole dashboard | `DashboardResponse` | full refetch, not optimistic update |

**Validation & authorization:** presence checks at [L69-L72](../../src/Billing.Api/Program.cs#L69-L72);
uniqueness at [store L68](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68). **Authorization:
none** — anyone can create a customer in the single global dataset.

**Persistence & side effects:** one dictionary insert ([store L73](../../src/Billing.Api/Infrastructure/BillingStore.cs#L73)); idempotency cache write. No webhook, no job.

**Tests:** **No direct coverage found** — there is no test project anywhere in the repo (verified by
file listing).

**Juniors miss:** that the `Idempotency-Key` is generated fresh per `postJson` call
([api.ts L154](../../src/Billing.Web/src/api.ts#L154)), so it protects against the *network* retrying
one request, not against a user double-clicking (two clicks → two keys → two customers, unless the
email-uniqueness check stops the second). **Seniors notice:** the idempotency cache and the business
write are not in one transaction — between the cache *miss* at
[L35](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L35) and the cache *set* at
[L66](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L66), a concurrent duplicate can
also miss and double-execute. (Risk [R-04](../08-reference/risk-register.md).)

**Drill:** trace what happens when the *same* `Idempotency-Key` is replayed with a *different* body.
Which line returns `409`? **Self-grade — Basic:** you find [L37-L42](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L37-L42).
**Solid:** you explain why it compares a body *hash*, not the body. **Strong:** you explain why the
duplicate-email check is a second, independent safety net and when it would *not* save you (different
email, same key+body would just replay; same email, different key → second insert rejected at the store).

---

## Flow 2: Metered billing cycle (the flagship — with worked arithmetic)

**Why it matters:** this is the product. It rolls up usage, applies a coupon, computes a total in
integer money, advances the period, and queues a webhook — the densest path in the codebase.

**Open first:** [`RunBillingCycle`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127) →
[`GenerateInvoice`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L113).

**Setup (from the seed):** the "Scale Usage" subscription `subA`
([store L305](../../src/Billing.Api/Infrastructure/BillingStore.cs#L305)) on the **Hybrid** "scale"
plan (base **$249**, included **100,000** units, unit price **$0.01**) with coupon **LAUNCH20**
(**20% off**), plus 8 seeded usage events ([store L316-L320](../../src/Billing.Api/Infrastructure/BillingStore.cs#L316-L320))
of `18000 + i*2500` units.

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Browser | [runBillingCycle](../../src/Billing.Web/src/App.tsx#L145-L150) | POST `/api/simulator/run-cycle` with `billingDate = now + 35 days` | `{billingDate}` | the 35-day jump is a *demo* hack ([hint L257](../../src/Billing.Web/src/App.tsx#L257)) |
| 2 | Endpoint | [Program.cs L204-L209](../../src/Billing.Api/Program.cs#L204-L209) | Calls `engine.RunBillingCycle(billingDate)` | — | no auth, no dry-run |
| 3 | Engine | [RunBillingCycle L118-L124](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L118-L124) | For each Active/Trialing sub whose `CurrentPeriodEnd <= billingDate`, generate one invoice | list of `Invoice` | **one invoice per run**, even if many periods elapsed (no catch-up) |
| 4 | Engine | [GenerateInvoice L36-L44](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L36-L44) | Flat/Hybrid → add a recurring line at `plan.BasePrice` | `InvoiceLine kind=recurring` | line currency = plan's; invoice currency = customer's ([L34](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L34)) |
| 5 | Store | [GetUnbilledUsage L178-L190](../../src/Billing.Api/Infrastructure/BillingStore.cs#L178-L190) | Select `InvoiceId == null` usage in `[periodStart, periodEnd)` | 8 events, sum 214,000 | **linear scan** of all usage |
| 6 | Engine | [L47-L60](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L47-L60) | `billable = max(0, total − included)`; add a usage line if > 0 | `InvoiceLine kind=usage` | included-units subtraction is per-run, not pro-rated |
| 7 | Engine | [L62-L64](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L64) | subtotal = Σ lines; discount = coupon; total = max(subtotal−discount, 0) | `Money` | currency-mismatch throws if customer ≠ plan/coupon currency |
| 8 | Engine | [L82-L88](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L88) | Save invoice; stamp usage with `InvoiceId`; advance subscription period | persisted | three writes, **no transaction** (fine for one lock; not for a DB) |
| 9 | Engine | [L90-L110](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L90-L110) | Enqueue an `invoice.created` outbound webhook | `WebhookEvent Pending` | enqueue + save not atomic with the invoice |

**The arithmetic (check this by hand):**

```
Usage total      = Σ (18000 + 2500·i), i=0..7   = 8·18000 + 2500·28 = 144000 + 70000 = 214,000 units
Billable units   = max(0, 214000 − 100000)       = 114,000 units
Recurring line   = $249.00                        = 24,900 minor units
Usage line       = 114,000 × $0.01                = 114,000 minor units   ($1,140.00)
Subtotal         = 24,900 + 114,000               = 138,900 minor units   ($1,389.00)
Discount (20%)   = round(138,900 × 0.20)          = 27,780 minor units    ($277.80)
TOTAL            = 138,900 − 27,780               = 111,120 minor units    = $1,111.20
```

The Starter subscription `subB` (Flat, $29, no coupon, period ends `now+15 ≤ now+35`) generates a
second invoice of **$29.00**. So `run-cycle` returns `{ generated: 2, invoices: [...] }`.

**Validation & authorization:** the engine guards canceled subs
([L16-L19](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L16-L19)) and missing customer/plan
([L21-L25](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L21-L25)). No authorization.

**Persistence & side effects:** invoice saved, 8 usage rows updated, subscription advanced, 1 webhook
queued — per invoice.

**Tests:** **No direct coverage found.** This worked total is exactly the kind of thing a unit test
should pin; module 05 has you write it.

**Juniors miss:** that usage is matched by `OccurredAt` falling in the *subscription's current
period* ([store L185-L186](../../src/Billing.Api/Infrastructure/BillingStore.cs#L185-L186)), not by
the `billingDate` — so usage recorded after the period end is silently deferred to next cycle.
**Seniors notice:** the period advance ([L84-L88](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L84-L88))
moves the window by exactly one cycle regardless of how far `billingDate` jumped — a subscription two
months overdue is under-billed by a month per run. (Risk [R-07](../08-reference/risk-register.md).)

**Drill:** add a 9th usage event of 5,000 units dated *inside* the period and recompute the total by
hand, then predict whether `subB` changes (it doesn't — Flat plan ignores usage). **Self-grade —
Basic:** new total is `$1,111.20 + round(5000×$0.01×0.8)`… compute it. **Solid:** you explain why the
discount applies to the *new* subtotal. **Strong:** you identify that the +5,000 only counts if its
`OccurredAt < CurrentPeriodEnd`.

---

## Flow 3: Pay an invoice (async background processing)

**Why it matters:** the one truly asynchronous user flow. The HTTP call returns `202 Accepted`
*before* the work is done; a worker finishes it later. This is the pattern every queue-backed system
uses.

**Open first:** [`QueuePayment`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L155) and
[`PaymentWorker.Process`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L27-L45).

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Browser | [payInvoice](../../src/Billing.Web/src/App.tsx#L152-L156) | POST `/api/invoices/{id}/pay` `{provider:"simulated"}` | — | UI shows "Pay" only for `Open` ([L281](../../src/Billing.Web/src/App.tsx#L281)) |
| 2 | Endpoint | [Program.cs L211-L221](../../src/Billing.Api/Program.cs#L211-L221) | `engine.QueuePayment` → `202 Accepted` + the pending `Payment` | `Payment Pending` | returns before payment actually happens |
| 3 | Engine | [QueuePayment L131-L139](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L131-L139) | Reject unknown / already-paid invoice | — | already-paid guard prevents double charge *here* |
| 4 | Engine | [L141-L153](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L141-L153) | Write a `Pending` payment; enqueue a `PaymentJob` available now | queue entry | — |
| 5 | Worker | [PaymentWorker L18-L23](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L18-L23) | Every 4s, drain all due jobs | — | drains entire queue per tick (could starve under load) |
| 6 | Worker | [Process L35-L44](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L35-L44) | Skip if already paid; else `ApplyPaymentResult(succeeded:true)` | — | **always succeeds** — no failure simulation |
| 7 | Engine | [ApplyPaymentResult L165-L179](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L165-L179) | Invoice → `Paid` + `PaidAt`; write a `Succeeded` payment | persisted | a *new* Payment row each call |
| 8 | Engine | [L181-L199](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L181-L199) | Enqueue `invoice.payment_succeeded` webhook | `WebhookEvent` | — |
| 9 | Worker | [WebhookDeliveryWorker L19-L23](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L19-L23) | Every 8s, drain webhook queue → mark `Succeeded` | — | "delivery" is simulated |
| 10 | Browser | refresh | The invoice flips to `Paid` only on the *next* dashboard pull | — | no live update; user must refresh |

**Validation & authorization:** invoice existence + not-already-paid
([L131-L139](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L131-L139)). No authorization — any
caller can pay any invoice.

**Persistence & side effects:** payment rows (one Pending, one Succeeded), invoice status change,
webhook enqueue + simulated delivery.

**Tests:** **No direct coverage found.**

**Juniors miss:** the `202 Accepted` means "accepted, not done" — the client can't read the result
synchronously. **Seniors notice:** `ApplyPaymentResult` is **not idempotent** — if it runs twice for
the same invoice (worker retry, or a provider webhook arriving for the same payment) it writes a
second `Succeeded` payment and a second outbound webhook ([R-05](../08-reference/risk-register.md)).

**Drill:** what stops a queued payment from charging an invoice that was *already paid* by the
inbound webhook between enqueue and processing? Find the guard. **Self-grade — Basic:** you find
[PaymentWorker L35-L38](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L35-L38). **Solid:** you
note it only guards the *invoice status*, not duplicate payment rows. **Strong:** you propose keying
payments by `(invoiceId, jobId)` to make the worker idempotent.

---

## Flow 4: Inbound signed webhook (the auth boundary)

**Why it matters:** the only endpoint with a real security gate. It's how an external payment
provider tells the system a payment settled. Get the signature check wrong and anyone can mark
invoices paid.

**Open first:** [`POST /api/webhooks/payment-provider`](../../src/Billing.Api/Program.cs#L223-L250)
and [`WebhookSignatureVerifier.IsValid`](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L40).

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Provider | (external) | POST raw JSON + `X-Webhook-Timestamp` + `X-Webhook-Signature` | raw body | — |
| 2 | Endpoint | [Program.cs L225-L228](../../src/Billing.Api/Program.cs#L225-L228) | `EnableBuffering`, read raw body, rewind | string | body read twice (also by idempotency mw) |
| 3 | Verifier | [IsValid L13-L28](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L13-L28) | Require both headers; parse unix ts; reject if older than 5 min | — | uses `DateTimeOffset.UtcNow` directly, not the clock |
| 4 | Verifier | [L30-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L30-L39) | HMAC-SHA256 over `{ts}.{body}`; **constant-time** compare | hex digest | secret defaults to `dev_secret_replace_me` |
| 5 | Endpoint | [L230-L233](../../src/Billing.Api/Program.cs#L230-L233) | Invalid → `401 Unauthorized` | — | correct status |
| 6 | Endpoint | [L235-L239](../../src/Billing.Api/Program.cs#L235-L239) | Deserialize `PaymentProviderWebhookRequest`; null → `400` | DTO | `EventId` is parsed but **never used for dedup** |
| 7 | Engine | [ApplyPaymentResult](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200) | Apply success/failure to the invoice | persisted | not idempotent on `EventId` |
| 8 | Endpoint | [L244](../../src/Billing.Api/Program.cs#L244) | `200 {received:true, eventId, eventType}` | — | — |

**Validation & authorization:** the HMAC signature **is** the authorization
([verifier L37-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37-L39)). The
5-minute timestamp tolerance limits replay, but there is **no nonce/event-id dedup**, so a valid
request replayed within the window is processed again.

**Persistence & side effects:** invoice status change + a new payment row + an outbound webhook —
same as Flow 3 step 7-8.

**Tests:** **No direct coverage found.** The webhook design doc has a working PowerShell signer you
can use to test by hand ([docs/webhooks.md](../../docs/webhooks.md#L30-L44)).

**Juniors miss:** that the signature covers `{timestamp}.{rawBody}` — sign the *raw* bytes, not the
re-serialized object, or it won't match. **Seniors notice:** the constant-time `FixedTimeEquals`
([L37](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37)) is the correct, non-obvious
choice — a naive `==` on the hex strings would leak timing. Good crypto, missing dedup.

**Drill:** an attacker captures a valid webhook and replays it 30 seconds later. Does it succeed?
Walk the verifier. **Self-grade — Basic:** yes, because it's within tolerance and the signature is
still valid. **Solid:** you note the invoice may already be `Paid` so the *effect* is muted but a
duplicate payment row is still written. **Strong:** you design the fix — persist seen `EventId`s and
reject duplicates, and explain why that belongs at the engine/store boundary, not the verifier.

---

## What these flows rotate through (coverage check)

| Concern | Covered by |
| --- | --- |
| React/client flow | Flow 1, 2 (UI trigger), 3 |
| ASP.NET write API | Flow 1 |
| Persistence | Flow 2 (usage scan), all (store writes) |
| Background/async | Flow 3 (payment), Flow 2/3 (webhook delivery) |
| Auth boundary | Flow 4 (HMAC) — and the *absence* of auth everywhere else |
| Money & domain logic | Flow 2 (the arithmetic) |

## Verification Notes

Flow 2's arithmetic was computed by hand from the seed constants and the `Money` rounding rules
([Money.Multiply L43-L47](../../src/Billing.Api/Domain/Money.cs#L43-L47)); it was **not** produced by
running the code. If you run the cycle and get a different number, trust the code and tell me which
step diverged — that's a real finding.
