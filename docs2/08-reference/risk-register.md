# Risk Register

Risks found by reading this repo. Because it is **synthetic**, this register is deliberately
aggressive — clean-looking generated code is exactly where missing auth, currency coupling, and
idempotency races hide. Each entry has evidence anchors, an impact/likelihood call, a cheap test to
prove it, a suggested fix, and a **confidence** level.

**Confidence legend:** `Confirmed` = the code path demonstrably does this (read-verified, not run).
`Hypothesis` = plausible from the code but needs a runtime check. Nothing here was executed (no .NET
runtime on the authoring host — see [verification-log](verification-log.md)).

| ID | Risk | Impact | Likelihood | Confidence |
| --- | --- | --- | --- | --- |
| R-01 | Currency coupling crashes metrics & invoicing for non-USD | High | Medium | Confirmed |
| R-02 | No authentication / authorization / tenant isolation (IDOR everywhere) | Critical | High | Confirmed |
| R-03 | Zero automated tests | Medium | High | Confirmed |
| R-04 | Idempotency: concurrent-duplicate execution window | High | Medium | Confirmed |
| R-05 | `ApplyPaymentResult` not idempotent → duplicate payments on retry/replay | High | Medium | Confirmed |
| R-06 | Webhook "outbox": dead retry path + `LastError` set on success + non-atomic enqueue | Medium | Medium | Confirmed |
| R-07 | Billing cycle has no catch-up → under-billing on missed periods | High | Medium | Confirmed |
| R-08 | In-memory store: data loss on restart, global-lock contention, non-atomic multi-write | High | High | Confirmed (by design) |
| R-09 | Committed default webhook signing secret | Medium | High | Confirmed |
| R-10 | `Money` assumes 2-decimal currencies | Medium | Low | Confirmed |
| R-11 | No dunning: failed payment leaves invoice `Open`, `PastDue` never set | Medium | Medium | Confirmed |
| R-12 | Weak input validation (email format, currency code, unbounded quantities) | Low | Medium | Confirmed |

---

### R-01 — Currency coupling crashes metrics & invoicing for non-USD

**Evidence:** [`CalculateMetrics`](../../src/Billing.Api/Program.cs#L262-L278) seeds the MRR and
outstanding accumulators with `Money.Zero("USD")` and then `sum.Add(...)` each subscription's plan
price / each invoice total. `Money.Add` throws `InvalidOperationException` on a currency mismatch
([EnsureSameCurrency L61-L67](../../src/Billing.Api/Domain/Money.cs#L61-L67)). The same coupling exists
in [`GenerateInvoice`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L34-L64): the invoice
currency is `customer.Currency` but line amounts come from `plan.BasePrice` (the plan's currency), and
the coupon's `AmountOff` is in the coupon's currency.
**Impact:** create one EUR plan with an active subscription, then `GET /api/dashboard` → unhandled
exception → 500 for *every* dashboard load (the whole UI is down, not just one row). A EUR customer on
a USD plan fails at invoice generation.
**Likelihood:** Medium — the seed is all-USD so it never triggers in the demo, but multi-currency is a
first-class field (`CreateCustomerRequest.Currency`, `CreatePlanRequest.Currency`).
**Suggested test:** integration test — POST a EUR plan + customer + subscription, then assert
`GET /api/dashboard` returns 200 (it currently won't).
**Suggested fix:** define a reporting currency and convert (FX layer), or group metrics by currency,
or constrain a subscription so customer/plan/coupon currencies must match (validate at
`AddSubscription`). The README already names this as a production gap
([architecture.md L18](../../docs/architecture.md#L18)).
**Confidence:** Confirmed (read-verified control flow; not executed).

### R-02 — No authentication / authorization / tenant isolation

**Evidence:** there is no `[Authorize]`, no authentication middleware, no policies, no tenant column
anywhere. Every route under [`MapGroup("/api")`](../../src/Billing.Api/Program.cs#L37-L250) is
anonymous and operates on a single global dataset. Any caller can read every customer
([dashboard L41-L63](../../src/Billing.Api/Program.cs#L41-L63)), pay any invoice
([pay L211-L221](../../src/Billing.Api/Program.cs#L211-L221)), or record usage against any
subscription ([usage-events L178-L202](../../src/Billing.Api/Program.cs#L178-L202)).
**Impact:** Critical for any real deployment — there is no notion of "my data". Every endpoint is an
IDOR (insecure direct object reference): supply any `Guid` and act on someone else's resource.
**Likelihood:** High — it's the default behavior of every endpoint.
**Suggested test:** none meaningful until auth exists; this is a design gap, not a flaky path.
**Suggested fix:** add authentication (JWT/cookie), a `TenantId`/`OwnerId` on entities, and an
authorization filter that scopes every store query to the caller's tenant. This is the single
biggest piece of missing architecture — see
[03/03](../03-architecture-and-patterns/03-validation-auth-and-permissions.md).
**Confidence:** Confirmed.

### R-03 — Zero automated tests

**Evidence:** no test project, no `*.Tests.csproj`, no test framework reference anywhere (verified by
full file listing). The worked invoice total in [Flow 2](../01-codebase-cartography/05-key-flows.md)
is exactly the kind of behavior nothing currently pins.
**Impact:** Medium directly, but it's a *multiplier* — every other risk here is undetectable on change
because nothing fails when behavior regresses.
**Likelihood:** High (it's a certainty).
**Suggested test:** the first one you write — pin `GenerateInvoice` for the seeded Scale subscription
to `$1,111.20`. See [05/02-writing-tests-here.md](../05-quality-engineering/02-writing-tests-here.md).
**Suggested fix:** add an xUnit project; start with engine unit tests (deterministic via the injected
clock) and a `WebApplicationFactory` smoke test.
**Confidence:** Confirmed.

### R-04 — Idempotency: concurrent-duplicate execution window

**Evidence:** [`IdempotencyMiddleware`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L35-L66)
checks the cache (`TryGet`, L35), then runs the handler, then writes the cache (`Set`, L66). Two
concurrent requests with the *same* key can both miss `TryGet` before either calls `Set`, so both
execute the business mutation.
**Impact:** High for payments — the protection that's supposed to prevent double-charge has a race.
**Likelihood:** Medium — needs genuine concurrency with the same key (e.g. an aggressive client retry
that fires before the first response).
**Suggested test:** fire two concurrent identical POSTs with one `Idempotency-Key`; assert only one
resource is created. (Today: not guaranteed.)
**Suggested fix:** write a "reserved" marker for the key *before* calling `next()` (so the second
request sees in-flight and waits/replays), or hold a per-key lock, or — better — move idempotency into
the same transaction as the mutation (see
[03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md)).
**Confidence:** Confirmed (the TryGet→Set gap is in the code).

### R-05 — `ApplyPaymentResult` not idempotent → duplicate payments

**Evidence:** [`ApplyPaymentResult`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200)
writes a **new** `Payment` row and enqueues a **new** outbound webhook on *every* call, keyed only by a
fresh `Guid`. It's reachable twice for one logical payment via (a) the `PaymentWorker` re-processing,
or (b) an inbound provider webhook replayed within the 5-minute window — the verifier accepts it and
`EventId` is parsed but **never used for dedup**
([webhook endpoint L235-L243](../../src/Billing.Api/Program.cs#L235-L243)).
**Impact:** High — duplicate `Succeeded` payment records and duplicate `payment_succeeded` webhooks
emitted downstream; reconciliation/accounting corruption.
**Likelihood:** Medium — provider webhooks are retried in the real world; the 5-min tolerance permits
replays.
**Suggested test:** call `ApplyPaymentResult` twice for one invoice; assert exactly one `Succeeded`
payment exists. POST the same signed webhook twice; assert one effect.
**Suggested fix:** persist processed `EventId`s (or a `(invoiceId, providerReference)` key) and reject
duplicates at the engine boundary; make the worker key payments by `jobId`.
**Confidence:** Confirmed.

### R-06 — Webhook "outbox": dead retry path, wrong `LastError`, non-atomic enqueue

**Evidence:** [`WebhookDeliveryWorker.Deliver`](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L26-L54)
wraps the delivery in `try/catch` with exponential backoff and requeue
([L41-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L41-L53)) — but the `try` body
([L28-L39](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L28-L39)) only builds a record
and calls `SaveWebhook`; nothing performs a real HTTP send, so it can't throw under normal operation
and **the entire catch/backoff/requeue branch is dead code**. Separately, `LastError` is set to a
"Simulated delivery…" note on the **success** path ([L35](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L35)),
which is semantically wrong (an error field populated on success). The enqueue is also not atomic with
the invoice save (separate locked store calls).
**Impact:** Medium — the reliability story is illusory; a future real HTTP send would exercise
untested backoff code, and the `LastError` field misleads operators reading the dashboard.
**Likelihood:** Medium.
**Suggested test:** inject an `IWebhookSender` that throws; assert the event is requeued with backoff
and `Status == Failed`. (Requires the seam to exist first.)
**Suggested fix:** introduce a real `IWebhookSender`; clear `LastError` on success; write the outbox
row and the business change in one transaction.
**Confidence:** Confirmed.

### R-07 — Billing cycle has no catch-up → under-billing

**Evidence:** [`RunBillingCycle`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127)
generates **one** invoice per eligible subscription, and `GenerateInvoice` advances the period by
exactly one cycle ([L84-L88](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L84-L88)) regardless
of how far `billingDate` is past `CurrentPeriodEnd`. A subscription three periods overdue gets billed
for one period and advanced one period.
**Impact:** High — silent revenue loss; periods are skipped, not deferred.
**Likelihood:** Medium — happens whenever the cycle runner misses runs (outage, infrequent schedule).
**Suggested test:** seed a subscription whose period ended 3 cycles ago; run one cycle; assert 3
invoices (or one invoice covering 3 periods), not 1.
**Suggested fix:** loop generation while `CurrentPeriodEnd <= billingDate`, or compute the number of
elapsed periods and bill them.
**Confidence:** Confirmed.

### R-08 — In-memory store: durability, contention, atomicity

**Evidence:** all state is dictionaries/queues in [`BillingStore`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L8-L17),
re-seeded on construction ([L19-L22](../../src/Billing.Api/Infrastructure/BillingStore.cs#L19-L22)). One
global lock serializes all access; multi-step operations (invoice save + usage stamp + period advance)
are separate locked calls, not one transaction.
**Impact:** High for production — restart loses all data; the global lock is a throughput ceiling; a
crash mid-`GenerateInvoice` can leave usage stamped but the period not advanced (or vice versa).
**Likelihood:** High (inherent to the design).
**Suggested test:** N/A (design) — but a characterization test before migrating (see
[Comparison 1 migration drill](../03-architecture-and-patterns/07-comparative-implementations.md)).
**Suggested fix:** the EF Core migration in Comparison 1, with real transactions around multi-write
operations. **Note:** this is *intended* for the learning repo ([README L57](../../README.md#L57)); it's
listed because it's the #1 thing that must change before this ships.
**Confidence:** Confirmed (by design).

### R-09 — Committed default webhook signing secret

**Evidence:** [`BillingOptions.WebhookSigningSecret`](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L5)
defaults to `dev_secret_replace_me`, and that literal is committed in
[appsettings.json L4](../../src/Billing.Api/appsettings.json#L4).
**Impact:** Medium — if shipped unchanged, anyone who reads the repo can forge valid inbound webhooks
and mark invoices paid.
**Likelihood:** High that it gets deployed as-is (it's the path of least resistance).
**Suggested test:** a startup check that fails if the secret equals the default in non-Development.
**Suggested fix:** load the secret from an environment variable / secret store; fail fast if missing
in production. (The frontend already does the right thing — `.env*` is gitignored.)
**Confidence:** Confirmed.

### R-10 — `Money` assumes 2-decimal currencies

**Evidence:** [`Money.MajorUnits`](../../src/Billing.Api/Domain/Money.cs#L29) and
[`FromMajor`](../../src/Billing.Api/Domain/Money.cs#L12-L16) hardcode `× 100` / `÷ 100`. JPY (0
decimals) and KWD (3 decimals) would be mis-scaled. The client repeats the assumption
([App.tsx L395](../../src/Billing.Web/src/App.tsx#L395)).
**Impact:** Medium for those currencies (10×–100× pricing errors).
**Likelihood:** Low while everything is USD.
**Suggested test:** `Money.FromMajor(1000m, "JPY").MinorUnits` should be `1000`, not `100000`.
**Suggested fix:** a per-currency minor-unit exponent table.
**Confidence:** Confirmed.

### R-11 — No dunning: failed payment leaves invoice `Open`, `PastDue` never set

**Evidence:** on failure, [`ApplyPaymentResult`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L165-L167)
sets the invoice back to `Open` and stops; nothing ever sets `SubscriptionStatus.PastDue`
([enum L19](../../src/Billing.Api/Domain/BillingEnums.cs#L19)) or retries. The `PaymentWorker` always
"succeeds" anyway ([L40-L44](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L40-L44)).
**Impact:** Medium — there's no collections lifecycle; a permanently failing invoice just sits `Open`.
**Likelihood:** Medium (it's an unbuilt feature, surfaced the first time a real payment fails).
**Suggested test:** apply a failed payment; assert the intended state transition (once defined).
**Suggested fix:** a dunning state machine (Open → PastDue → retries → Uncollectible) — a good
[senior build project](../06-contribution-practice/03-senior-build-projects.md).
**Confidence:** Confirmed (it's a scope gap, flagged as a reliability risk).

### R-12 — Weak input validation

**Evidence:** customer creation checks email *presence* but not *format*
([L69-L72](../../src/Billing.Api/Program.cs#L69-L72)); currency is normalized (uppercased) but not
validated against ISO codes ([NormalizeCurrency L292-L293](../../src/Billing.Api/Program.cs#L292-L293)),
so `"ZZZ"` is accepted; usage quantity must be `> 0` but has no upper bound
([L180-L183](../../src/Billing.Api/Program.cs#L180-L183)).
**Impact:** Low — bad data, not a breach; but a 2-billion-unit usage event would produce an absurd
invoice.
**Likelihood:** Medium.
**Suggested test:** POST a customer with `"not-an-email"`; assert 400 (today it's 201).
**Suggested fix:** validate email format, currency against a known set, and bound quantities — ideally
via a validation library or model annotations.
**Confidence:** Confirmed.

## How to use this register

- **Mid-level:** pick R-12 or R-10 — small, testable, real. Write the failing test first.
- **Senior:** R-01, R-02, R-05, R-07 are the ones that would matter in production. Sequence them in
  [06/03 senior projects](../06-contribution-practice/03-senior-build-projects.md) and decide which you'd
  do first if you owned this for three months (see [the critique](../03-architecture-and-patterns/06-architecture-critique.md)).

## Verification Notes

Every entry is read-verified from the anchored code on 2026-06-09 and labeled `Confirmed` where the
code path demonstrably produces the behavior. None were reproduced at runtime (no .NET SDK). The
"create a EUR plan and watch the dashboard 500" claims (R-01) are the highest-value things for *you*
to confirm by hand once you can run it — that's the first item in the
[verification-log open uncertainties](verification-log.md).
