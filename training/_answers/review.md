# Sealed answer — Blind Code Review (SubscriptionBillingDashboard / .netstripe)

Do not open this before attempting `training/review/EXERCISE.md`. 19 findings, worst first.

---

### 0. [BLOCKER] The repo does not compile as committed — two separate errors
- **Category**: correctness / build hygiene
- **Evidence**: `dotnet build src/Billing.Api` fails on a clean checkout of `main`
  (commit `a84f11b`) with two independent errors:
  1. `CS0246: The type or namespace name 'IBillingClock' could not be found` at
     `Infrastructure/IdempotencyMiddleware.cs:90` (the `IdempotencyStore(IBillingClock
     clock)` constructor). The file is missing `using Billing.Api.Domain;` — every other
     file in `Infrastructure/` that references `IBillingClock` (e.g. `BillingEngine.cs`,
     `PaymentWorker.cs`) has that using; this one doesn't.
  2. `CS0173: Type of conditional expression cannot be determined because there is no
     implicit conversion between '<null>' and 'Money'` at `Program.cs:143`, inside
     `POST /api/coupons`:
     `var amountOff = request.AmountOff is null ? null : Money.FromMajor(...);` — `Money`
     is a `readonly record struct` (`Domain/Money.cs:5`), so the compiler can't infer a
     common type between the `null` literal and a non-nullable struct without an explicit
     `(Money?)null` or a declared `Money?` local.
- **Why it matters**: a fresh clone cannot be built at all — not "has a bug in the coupon
  endpoint," but literally does not produce a binary. If you found this in the first
  minute by trying to build, that's the right instinct: **always try to build before
  trusting anything else about a repo's "working-looking" state.** The two curriculum
  trees (`docs/upskill/`, `docs2/`) describe extensive, specific system behavior —
  including the coupon endpoint — for code that has never successfully compiled in this
  checkout.
- **Fix direction**: add `using Billing.Api.Domain;` to
  `Infrastructure/IdempotencyMiddleware.cs`; change the coupon ternary to
  `request.AmountOff is null ? (Money?)null : Money.FromMajor(...)`. Two one-line fixes.

### 1. [BLOCKER] Duplicate invoice generation race in the billing cycle
- **Category**: concurrency / data integrity / money
- **Evidence**: `Infrastructure/BillingEngine.cs:115-127` (`RunBillingCycle`) checks
  `subscription.CurrentPeriodEnd <= billingDate` then calls `GenerateInvoice`, which does
  several *separate* `BillingStore` calls (`BillingStore.cs`: `TryGetSubscription`,
  `SaveInvoice`, `SaveUsageEvents`, `SaveSubscription` each take and release their own
  `lock (_gate)` individually — there is no lock spanning the whole
  read-decide-invoice-advance-period sequence).
- **Why it matters**: two overlapping calls to `POST /api/simulator/run-cycle` (a UI
  double-click, a retried cron trigger, a load balancer replaying a slow request) can both
  see the same subscription's old `CurrentPeriodEnd`, both pass the `<=` check, and both
  generate an `Invoice` for the same billing period before either write lands. Customers
  get billed twice for one period. This is the same class of bug as "double-charge on
  retry" — the classic idempotency failure — except here it's a background/cron path, not
  an HTTP client retry, so an `Idempotency-Key` header wouldn't have saved it anyway.
- **Fix direction**: make the read-check-write a single atomic unit (hold the store lock —
  or move to a real DB with a `WHERE current_period_end <= @billingDate` conditional
  update — across the whole subscription-advance step), and/or derive a natural
  idempotency key from `(subscriptionId, periodStart)` and use `INSERT ... ON CONFLICT
  DO NOTHING` semantics before ever calling `GenerateInvoice`.

### 2. [HIGH] Payment-provider webhook has no event-level idempotency
- **Category**: idempotency / money
- **Evidence**: `Program.cs:223-250` (`POST /api/webhooks/payment-provider`) is a `POST`
  under `/api`, so `IdempotencyMiddleware.RequiresIdempotency` (line 70-72) returns true —
  but the middleware only protects requests that send an `Idempotency-Key` header
  (`IdempotencyMiddleware.cs:17-21`: missing header → `await next(context); return;`, no
  protection). Real payment providers retry webhook delivery on any non-2xx/timeout and
  identify retries by the event's own id, not a generic idempotency header.
  `Contracts/Requests.cs`'s `PaymentProviderWebhookRequest.EventId` is parsed and even
  echoed back in the response (`Program.cs:244`) but is **never checked against anything
  already processed.**
- **Why it matters**: a retried webhook delivery calls `BillingEngine.ApplyPaymentResult`
  twice for the same invoice (`BillingEngine.cs:157-200`), each call creates a new
  `Payment` row (`Guid.NewGuid()` at line 171) and queues a new
  `invoice.payment_succeeded`/`invoice.payment_failed` webhook (lines 181-199) — double
  revenue recognition in any downstream ledger, and the customer/ops sees the event twice.
- **Fix direction**: dedupe on `payload.EventId` (a `HashSet<string>`/store table keyed by
  event id, checked and marked *before* calling `ApplyPaymentResult`, inside the same lock
  as the mark — not check-then-process-then-mark).

### 3. [HIGH] `PaymentWorker` and the real payment webhook race and contradict each other
- **Category**: correctness / money
- **Evidence**: `Infrastructure/PaymentWorker.cs:27-45` hardcodes `succeeded: true` (line
  42) for every dequeued payment job — it *simulates* success unconditionally, on a timer
  (`PaymentWorkerIntervalSeconds`, default 4s, `BillingOptions.cs:6`) — independent of
  whatever the real provider later reports through
  `POST /api/webhooks/payment-provider` → `ApplyPaymentResult`.
- **Why it matters**: if the real provider later reports a decline, `ApplyPaymentResult`
  (`BillingEngine.cs:157-179`) unconditionally overwrites `Invoice.Status` back to `Open`
  and appends a second, contradictory `Payment` row and a second, contradictory webhook —
  a customer can see "paid" flip to "payment failed" seconds after checkout, and there are
  now two `Payment` records disagreeing about the same invoice with no reconciliation.
- **Fix direction**: `PaymentWorker` should call the (mocked) payment provider and honor
  its actual response, not hardcode success; at minimum it should not fire if a real
  provider webhook already resolved the invoice.

### 4. [HIGH] Webhook "delivery" never delivers anything, to a hardcoded fake URL
- **Category**: fake integration (AI-generated smell) / operability
- **Evidence**: `Infrastructure/WebhookDeliveryWorker.cs:26-39` — `Deliver` always marks
  the event `WebhookDeliveryStatus.Succeeded` and writes
  `LastError = "Simulated delivery. Replace with durable HTTP delivery in production."`;
  no HTTP call is made anywhere in this class. Every webhook enqueued by
  `BillingEngine.GenerateInvoice`/`ApplyPaymentResult` (`BillingEngine.cs:100-110`,
  `181-199`) targets the literal string `"https://example.com/webhooks/billing"` —
  hardcoded, not read from `BillingOptions`, not per-customer/tenant.
- **Why it matters**: the dashboard's `WebhookEvents` list (surfaced via
  `GET /api/dashboard`, `Program.cs:59-61`) shows every event as `Succeeded`, which looks
  done to anyone reading the UI or the code casually — this is exactly the
  "fake integration presented as working" smell the review brief calls out.
- **Fix direction**: either implement real outbound HTTP delivery with retry/backoff and a
  per-customer destination URL, or clearly mark this as a stub (`NotImplemented` status,
  not `Succeeded`) so nothing downstream is misled.

### 5. [HIGH] No authentication or authorization anywhere
- **Category**: security (OWASP A01 Broken Access Control)
- **Evidence**: every `MapGet`/`MapPost` under `api` in `Program.cs:37-250` is
  unauthenticated — no `[Authorize]`, no API key middleware, no principal check anywhere
  in `Program.cs` or `IdempotencyMiddleware.cs`. `GET /api/dashboard` (`Program.cs:41-63`)
  returns every customer's name, email, every invoice total, every payment, and every
  webhook payload to any caller.
- **Why it matters**: this is a billing dashboard — financial PII and revenue data with no
  access control at all. CORS (`Program.cs:21-30`) only restricts *browser* origins to
  `localhost:5173`/`127.0.0.1:5173`; it does nothing against a direct API client.
- **Fix direction**: add authn (API key or JWT) and scope every read/write to the calling
  tenant/customer before this is anything but a local demo.

### 6. [MEDIUM] Idempotency state and job queues are process-local and non-durable
- **Category**: reliability / operability
- **Evidence**: `IdempotencyStore` (`IdempotencyMiddleware.cs:90-120`) is an
  `AddSingleton` in-memory `Dictionary`; `BillingStore._paymentJobs` and `_webhookQueue`
  (`BillingStore.cs:15,17`) are plain in-memory `Queue<T>`, also singleton.
- **Why it matters**: any restart/redeploy silently drops every in-flight idempotency
  cache entry, every queued payment job, and every queued webhook — no persistence, no
  dead-letter queue, no replay. A deploy during a billing cycle loses money-moving state
  with no trace.
- **Fix direction**: back these with a real store (Postgres/Redis) before this leaves a
  single-instance demo.

### 7. [MEDIUM] Idempotency cache key has no caller/tenant scoping
- **Category**: security / correctness
- **Evidence**: `cacheKey` (`IdempotencyMiddleware.cs:33`) is
  `"{method}:{path}:{idempotencyKey}"` — no authenticated-principal component (compounds
  finding 5: there is no principal at all). Any caller can supply a colliding
  `Idempotency-Key` and either replay another caller's cached response or get spurious
  409s by reusing a key someone else already used.
- **Fix direction**: once auth exists, key the idempotency cache by
  `(principal, method, path, idempotencyKey)`, not just the client-supplied key.

### 8. [MEDIUM] Failed requests are never cached, so retry-after-failure is invisible to the client
- **Category**: idempotency / API design
- **Evidence**: `IdempotencyMiddleware.cs:63-67` only stores the response when
  `context.Response.StatusCode is >= 200 and < 300`. A client that gets a 500/other error
  has no cached record telling it whether the write actually landed before failing, and
  combined with finding 1's non-atomic writes there's no way to safely distinguish
  "definitely didn't happen" from "happened, then the response was lost."
- **Fix direction**: this is a legitimate design choice for many APIs (don't cache
  failures so genuine retries can succeed) but it needs to be paired with idempotent
  writes at the storage layer (see finding 1), which this app doesn't have yet.

### 9. [MEDIUM] Hardcoded webhook-signing secret default ships in source
- **Category**: security (hardcoded secret / AI-generated smell)
- **Evidence**: `Infrastructure/BillingOptions.cs:5`:
  `WebhookSigningSecret { get; init; } = "dev_secret_replace_me";` — nothing at startup
  checks whether the configured value still equals this literal default, or refuses to
  start/verify if so.
- **Why it matters**: if this ever runs with unset configuration, every request signed
  with the well-known default secret verifies successfully in
  `WebhookSignatureVerifier.IsValid` (`WebhookSignatureVerifier.cs:33`) — a forged webhook
  can flip any invoice to paid.
- **Fix direction**: fail startup if the secret equals the placeholder in non-Development
  environments.

### 10. [MEDIUM] Webhook replay-window check bypasses the injected clock
- **Category**: testability / consistency
- **Evidence**: `WebhookSignatureVerifier.cs:25` calls `DateTimeOffset.UtcNow` directly,
  while every other time-dependent path in the app uses `IBillingClock`
  (`Domain/BillingClock.cs`, injected into `BillingEngine`, `PaymentWorker`,
  `WebhookDeliveryWorker`, `IdempotencyStore`). This one class can't be tested against a
  fixed clock without real wall-clock sleeps.
- **Fix direction**: inject `IBillingClock` here too.

### 11. [MEDIUM] `Coupon.CalculateDiscount` computes the same expression twice, fragile to future edits
- **Category**: maintainability / correctness risk
- **Evidence**: `Domain/BillingModels.cs:31-49` — the final `return` recomputes
  `percentDiscount.Add(amountDiscount)` a second time in the ternary's true/false branches
  instead of binding it to a local once. Traced by hand, both branches currently agree —
  this is not a live bug today — but any future edit to one occurrence without the other
  will silently desynchronize the over-discount guard from the value actually returned.
- **Fix direction**: bind `var combined = percentDiscount.Add(amountDiscount);` once and
  reuse it.

### 12. [MEDIUM] Metered usage pools all `Metric` values into one bill line
- **Category**: correctness / domain modeling
- **Evidence**: `BillingEngine.cs:46-60` — `totalUsage = unbilledUsage.Sum(x =>
  x.Quantity)` sums every `UsageEvent` for the subscription regardless of
  `UsageEvent.Metric` (an arbitrary string per event, `BillingModels.cs:66`), against a
  single `plan.IncludedUnits`/`plan.UnitPrice`; only the *first* event's `Metric` name is
  used for the invoice line description (`unbilledUsage.FirstOrDefault()?.Metric`, line
  54).
- **Why it matters**: a plan billing two distinct metered metrics (e.g. `api_calls` and
  `storage_gb`) would have them priced identically and pooled under one label — either a
  missing feature (if multi-metric metering was intended) or a landmine waiting for
  someone to add a second metric string.

### 13. [MEDIUM] Subscription cancellation isn't implemented; unbilled usage on a canceled subscription is unrecoverable
- **Category**: correctness / missing feature
- **Evidence**: `SubscriptionStatus` declares `Canceled`/`PastDue`
  (`BillingEnums.cs:16-22`), but no endpoint in `Program.cs` ever transitions a
  subscription to those states, and `RunBillingCycle` only processes `Active`/`Trialing`
  subscriptions (`BillingEngine.cs:118`). There is no final-invoice / write-off code path
  for whatever future cancellation flow lands.
- **Why it matters**: if/when cancellation is added, any usage events recorded before it
  will be silently unbillable unless this is handled explicitly.

### 14. [LOW] Invoice numbers are `Guid`-derived, not sequential
- **Evidence**: `BillingEngine.cs:205-206`. Fine for a demo; several jurisdictions require
  gapless, sequential invoice numbering for tax compliance — worth a note, not a blocker
  here.

### 15. [LOW] Background workers retry forever with no dead-letter path or alerting
- **Evidence**: `WebhookDeliveryWorker.cs:40-53` — exponential backoff capped at 300s,
  `Attempt` grows unbounded, failures only surface as `LogWarning`. No metric, no cap, no
  operator-visible "stuck" signal.

### 16. [LOW] No correlation id / structured logging
- **Evidence**: no request id, idempotency key, or webhook event id is attached to any log
  statement anywhere in `Infrastructure/`. Tying a dashboard metrics anomaly back to a
  specific request or webhook would require manual timestamp correlation.

### 17. [LOW] Zero test files, despite two copies of a "testing strategy" doc
- **Category**: AI-generated smell (docs claiming things the code doesn't do)
- **Evidence**: `find -iname "*test*"` under the repo returns only
  `docs/upskill/05-quality-engineering/01-testing-strategy.md` and
  `docs2/05-quality-engineering/01-testing-strategy.md` (plus their sibling
  `02-writing-tests-here.md` files) — no `*Test*.cs`, no test project in the `.sln`. The
  curriculum discusses testing a codebase that has never been exercised by an automated
  test.

### 18. [LOW] `Billing.Web` is not part of the solution file
- **Evidence**: `SubscriptionBillingDashboard.sln` lists only `Billing.Api`; the frontend
  under `src/Billing.Web` builds independently per `README.md`. Anyone opening the `.sln`
  in an IDE never sees the frontend exists. Minor onboarding gap, correctly caught by
  `docs2/01-codebase-cartography/01-system-map.md`.

---

## Which findings are "old-AI-generated" smells specifically
- **#4** — fake/simulated webhook delivery marked `Succeeded`, hardcoded destination URL.
- **#9** — hardcoded fallback secret with no startup guard.
- **#17** — docs describe a testing strategy for code with zero tests.
- **#3** — a background worker that *always* simulates success is a "looks complete,
  isn't" pattern: it makes the payment flow appear to work end-to-end in a demo while
  quietly being disconnected from the real provider-confirmation path.

## Severity ranking rationale (worst first)
Money correctness under concurrency (#1) and unguarded webhook replay (#2, #3) outrank
security (#5, #9) only because #1-3 corrupt financial records *silently and immediately*
under normal-looking operation (no attacker required), whereas #5/#9 require someone to
exploit the gap. Both classes are release blockers for anything beyond a local demo.
