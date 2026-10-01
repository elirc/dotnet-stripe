# 06.01 — Good-First Tickets

Eight small, safe, real contributions spread across the repo. Each is additive or local, follows an
existing pattern, and ships with a test. Difficulty: **Easy** unless noted.

> Reminder: simulated OSS workflow — treat the "Review questions" as what a maintainer will actually
> ask on your PR.

---

## Ticket GF-1: Add `GET /api/coupons/{id}` lookup
**Difficulty:** Easy · **Time:** 30 min · **Skills:** minimal APIs, the store's Try-accessors
**Story:** As an API consumer, I want to fetch one coupon by id so that I don't have to download the
whole list.
**Why it's a good contribution:** read-only, additive, one file, mirrors an existing pattern — tiny
blast radius. Rejected if it bypasses the store lock or returns 200-with-null on a miss.
**Acceptance criteria:**
- [ ] `GET /api/coupons/{id:guid}` returns 200 + the coupon, or 404 with the standard `{error}` envelope.
- [ ] A test covers hit and miss.
**Read first:** [`GET /api/coupons` L122](../../src/Billing.Api/Program.cs#L122), [`TryGetCoupon` L142-L145](../../src/Billing.Api/Infrastructure/BillingStore.cs#L142-L145), and the 404 envelope at [subscriptions L159](../../src/Billing.Api/Program.cs#L159).
**Files likely touched:** [Program.cs](../../src/Billing.Api/Program.cs) — add one route.
**Implementation plan:** 1. Add the route after the coupons GET. 2. Use `TryGetCoupon`. 3. Match the existing NotFound shape. 4. Test.
**Illustrative fake-code shape:**
```csharp
// Illustrative: not from this repo
api.MapGet("/coupons/{id:guid}", (Guid id, BillingStore store) =>
    store.TryGetCoupon(id, out var c) ? Results.Ok(c) : Results.NotFound(new { error = $"Unknown coupon: {id}" }));
```
**What could go wrong / checks / review questions:** Does a missing coupon return 404, not 200? Is the route `{id:guid}` so a non-guid 404s at routing? Why reuse `TryGetCoupon` instead of `Coupons().FirstOrDefault`?

---

## Ticket GF-2: Add `GET /api/customers/{id}` and `GET /api/subscriptions/{id}`
**Difficulty:** Easy · **Time:** 30 min · **Skills:** same as GF-1
**Story:** As an API consumer, I want single-resource lookups for customers and subscriptions.
**Why it's good:** completes an obvious gap consistently; reuses `TryGetCustomer`/`TryGetSubscription`.
**Acceptance criteria:** both endpoints return 200/404 with the standard envelope; tests for each.
**Read first:** [`TryGetCustomer` L132-L135](../../src/Billing.Api/Infrastructure/BillingStore.cs#L132-L135), [`TryGetSubscription` L147-L150](../../src/Billing.Api/Infrastructure/BillingStore.cs#L147-L150).
**Files likely touched:** [Program.cs](../../src/Billing.Api/Program.cs).
**Review questions:** Are the two endpoints consistent with each other and GF-1? Same envelope everywhere?

---

## Ticket GF-3: Validate email format on customer create
**Difficulty:** Easy · **Time:** 45 min · **Skills:** validation, regex/`MailAddress`
**Story:** As an operator, I want malformed emails rejected so the customer list stays clean. (R-12)
**Why it's good:** closes a real validation gap; localized to one handler; testable.
**Acceptance criteria:**
- [ ] `POST /api/customers` with `"not-an-email"` returns 400 with a clear message.
- [ ] A valid email still succeeds. Tests for both.
**Read first:** the current presence-only check at [L69-L72](../../src/Billing.Api/Program.cs#L69-L72).
**Files likely touched:** [Program.cs](../../src/Billing.Api/Program.cs).
**Illustrative fake-code shape:**
```csharp
// Illustrative: not from this repo
if (!System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))
    return BadRequest("Customer email is not a valid address.");
```
**What could go wrong / review questions:** Don't over-validate (RFC email regexes are notoriously
wrong) — `MailAddress.TryCreate` is pragmatic. Does the message avoid leaking internals? Should this
move to a shared validator if other endpoints need email later?

---

## Ticket GF-4: Validate currency against an allowlist
**Difficulty:** Easy · **Time:** 45 min · **Skills:** validation, the money boundary
**Story:** As an operator, I want only supported currencies accepted so we don't create `"ZZZ"` plans. (R-12)
**Why it's good:** prevents bad data at the edge; the allowlist also documents what the app supports.
**Acceptance criteria:** unsupported currency → 400; `USD`/`EUR`/`GBP` (say) accepted; tests.
**Read first:** [`NormalizeCurrency` L292-L293](../../src/Billing.Api/Program.cs#L292-L293) (note it only
uppercases) and [Money's currency guard L20-L23](../../src/Billing.Api/Domain/Money.cs#L20-L23).
**Files likely touched:** [Program.cs](../../src/Billing.Api/Program.cs) (apply on customer/plan/coupon create).
**Review questions:** Where should the allowlist live (config vs constant)? Does adding `EUR` here
*expose* R-01 (the dashboard 500)? **Yes** — note that in the PR; this ticket should be paired with, or
explicitly defer to, the R-01 fix.

---

## Ticket GF-5: Bound usage-event quantity
**Difficulty:** Easy · **Time:** 30 min · **Skills:** validation
**Story:** As an operator, I want absurd usage quantities rejected so invoices stay sane. (R-12)
**Why it's good:** one-line guard, prevents an `int`-overflow-shaped problem in the rollup.
**Acceptance criteria:** quantity above a sane max (e.g. 100M) → 400; tests at the boundary.
**Read first:** the rollup that consumes it — [engine L49-L50](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L49-L50) — and the current check [L180-L183](../../src/Billing.Api/Program.cs#L180-L183).
**Review questions:** What's a defensible upper bound and why? Does the limit belong in config?

---

## Ticket GF-6: Stop setting `LastError` on successful webhook delivery
**Difficulty:** Easy · **Time:** 30 min · **Skills:** reading records, semantics
**Story:** As an operator reading the dashboard, I want `LastError` to mean "the last error", not a note
on success. (part of R-06)
**Why it's good:** a genuine semantic bug fix, tiny, with a clear before/after.
**Acceptance criteria:**
- [ ] On success, `LastError` is `null` (or a separate `Note` field if you prefer to keep the message).
- [ ] The dashboard's webhook table no longer shows a "Simulated delivery" string in the *error* column for delivered events.
**Read first:** the success path that sets `LastError` at [worker L30-L37](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L30-L37), surfaced at [App.tsx L352](../../src/Billing.Web/src/App.tsx#L352).
**Files likely touched:** [WebhookDeliveryWorker.cs](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs) (and optionally the UI label).
**Review questions:** Is the message useful enough to keep in a *separate* field? Does the UI still read
sensibly for both delivered and failed rows?

---

## Ticket GF-7: Make `InvoiceLine.Kind` an enum
**Difficulty:** Medium · **Time:** 1–2 h · **Skills:** records, serialization, the DTO boundary
**Story:** As a developer, I want `Kind` to be a typed enum (`Recurring`/`Usage`) so typos are
impossible and the wire value is stable.
**Why it's good:** removes stringly-typed data; touches the domain → DTO → TS chain, so it teaches the
boundary. Medium because it crosses layers.
**Acceptance criteria:**
- [ ] `InvoiceLine.Kind` is an enum; serialized as a string (the app already uses `JsonStringEnumConverter`, [Program.cs L10-L13](../../src/Billing.Api/Program.cs#L10-L13)).
- [ ] The two construction sites updated; the React type updated; tests confirm the wire value.
**Read first:** [`InvoiceLine` L71-L76](../../src/Billing.Api/Domain/BillingModels.cs#L71-L76), the two
`Kind` strings (`"recurring"`/`"usage"`) at [engine L43, L58](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L43-L58), and the DTO at [Responses.cs L30-L35](../../src/Billing.Api/Contracts/Responses.cs#L30-L35).
**Review questions:** Does the serialized string stay backward-compatible (`"Recurring"` vs the old
`"recurring"`)? This is a **public-contract change** — call it out in the PR.

---

## Ticket GF-8: Log inbound webhook accept/reject
**Difficulty:** Easy · **Time:** 45 min · **Skills:** `ILogger`, the security boundary
**Story:** As an on-call engineer, I want every inbound webhook attempt logged (accepted or rejected)
so a forged-signature attack leaves a trace. (observability gap)
**Why it's good:** small, additive, closes a real blind spot ([401s are silent today](../../src/Billing.Api/Program.cs#L230-L233)).
**Acceptance criteria:**
- [ ] A `Warning` log on signature rejection (with the event id if parseable) and an `Information` log on accept.
- [ ] No secret or full body is logged.
**Read first:** the webhook endpoint [L223-L250](../../src/Billing.Api/Program.cs#L223-L250); note it has
no logger injected yet — you'll add one.
**Review questions:** Are you logging *enough* to investigate but *not* leaking the body/secret? Is the
log level right (reject = Warning)?

---

## How to ship any of these (the loop)

1. Branch. 2. Write the failing test first (per [05/02](../05-quality-engineering/02-writing-tests-here.md)).
3. Make it pass. 4. Self-review against [07/01](../07-career-and-collaboration/01-code-review-mindset.md).
5. Write the PR with [07/02's template](../07-career-and-collaboration/02-writing-prs-and-rfcs.md),
naming the pattern you followed and the blast radius.

## Verification Notes

Every "read first" anchor was opened on 2026-06-09. Illustrative code is labeled and matches the real
method signatures/envelopes but was **not** compiled (no .NET SDK). GF-4's interaction with R-01 is the
kind of cross-effect a good contributor surfaces — it's called out deliberately.
