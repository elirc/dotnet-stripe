> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/03-architecture-and-patterns/05-pattern-catalog.md.txt).

# 03.05 — Pattern Catalog

Nine patterns this repo uses well enough to learn from. Each card: the problem it solves, the general
shape, the **real** anchored example here, why it works, how it fails, and a drill. Steal the shape,
not the line.

---

## Pattern: Value Object (money as an integer type)

**Problem:** representing money as `double`/`decimal` invites rounding drift, currency mixups, and
scattered formatting.
**General shape:** a small immutable type that wraps a primitive, enforces its invariants in the
constructor, and exposes only safe operations.

**Real example:** [`Money`](../../src/Billing.Api/Domain/Money.cs#L5-L68) — `readonly record struct`,
`long MinorUnits` + `Currency`, constructor rejects blank currency
([L20-L23](../../src/Billing.Api/Domain/Money.cs#L20-L23)), arithmetic guards currency match
([EnsureSameCurrency L61-L67](../../src/Billing.Api/Domain/Money.cs#L61-L67)).
**Second example:** none — `Money` is the only value object; everything else is an entity record.

**Why it works:** all money math is integer math, so `$0.10 + $0.20` is exactly `30` minor units, not
`0.30000000000000004`. Currency can't be silently mixed — `Add` throws.
**Failure modes:** the `MajorUnits` accessor hardcodes `/ 100`
([L29](../../src/Billing.Api/Domain/Money.cs#L29)), so 3-decimal (KWD) or 0-decimal (JPY) currencies
are wrong. `Multiply` rounds *away from zero* every time ([L45](../../src/Billing.Api/Domain/Money.cs#L45)),
which can over-collect on long invoices.
**Use when:** any domain with money, quantities, or units that must not drift.
**Avoid when:** the value has no invariants worth protecting (don't wrap a plain `string Name`).
**Drill:** add `JPY` (0 decimals) support without breaking `USD`. Where does the `/ 100` assumption
need to become per-currency? List every site.

---

## Pattern: Immutable entity + non-destructive update

**Problem:** mutable entities make it hard to reason about who changed what, and invite partial
updates that violate invariants.
**General shape:** model entities as immutable records; "change" produces a new instance via `with`.

**Real example:** every domain type is a `sealed record`
([BillingModels.cs](../../src/Billing.Api/Domain/BillingModels.cs)). Updates use `with`:
[advance the subscription period](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L84-L88),
[mark an invoice paid](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L165-L167),
[stamp usage with an invoice id](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L83).
**Second example:** [webhook status transition](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L30-L37).

**Why it works:** you never see a half-updated invoice; a "change" is a new object you save
explicitly, so the write site is obvious and greppable.
**Failure modes:** every update is a full read-modify-**save** — forget the save and the change
vanishes (the store hands out copies, [e.g. L51](../../src/Billing.Api/Infrastructure/BillingStore.cs#L51)).
With a real DB this becomes the lost-update problem unless you add optimistic concurrency.
**Use when:** correctness matters more than write throughput; domain logic is rule-heavy.
**Avoid when:** you mutate huge objects in tight loops (records allocate a fresh copy each `with`).
**Drill:** find a place where a `with` result is computed but never saved. (Hint: it isn't here — but
prove it by listing every `with` and matching it to a `Save*`.)

---

## Pattern: Injected clock (the testability seam)

**Problem:** code that calls `DateTime.UtcNow` directly is untestable — you can't make "now" be a
fixed instant.
**General shape:** define an interface for "current time" and inject it; production uses the system
clock, tests use a fake.

**Real example:** [`IBillingClock`](../../src/Billing.Api/Domain/BillingClock.cs#L3-L11), registered
as a singleton ([Program.cs L14](../../src/Billing.Api/Program.cs#L14)), threaded through the engine,
store, idempotency store, and workers.
**Second example:** none needed — it's used consistently… *almost*.

**Why it works:** it's the one seam that would make the whole billing engine deterministically
testable. Inject a clock fixed at a known instant and the invoice arithmetic in
[Flow 2](../01-codebase-cartography/05-key-flows.md) becomes a pinned unit test.
**Failure modes:** the abstraction leaks — [`WebhookSignatureVerifier`](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L25)
reads `DateTimeOffset.UtcNow` directly, so its 5-minute tolerance can't be tested deterministically.
One bypass defeats the seam.
**Use when:** any time-dependent logic you'll want to test (expiry, scheduling, TTLs).
**Avoid when:** never, really — but don't over-abstract trivial timestamps in throwaway code.
**Drill:** make the signature verifier testable. What's the minimal change, and does it widen the
constructor? (See [comparison of DI choices](07-comparative-implementations.md).)

---

## Pattern: DTO boundary (wire contract ≠ domain model)

**Problem:** exposing entities directly couples your HTTP contract to internal storage shapes — every
refactor risks a breaking API change.
**General shape:** map domain objects to response DTOs at the edge; never serialize entities raw.

**Real example:** [`InvoiceResponse.From`](../../src/Billing.Api/Contracts/Responses.cs#L53-L73) maps
`Invoice` → a wire DTO with `MoneyDto` (which pre-formats money for the client). Used at
[the invoices endpoint](../../src/Billing.Api/Program.cs#L176) and inside the
[dashboard](../../src/Billing.Api/Program.cs#L58).
**Second example:** [`MoneyDto.From`](../../src/Billing.Api/Contracts/Responses.cs#L7-L8).

**Why it works:** the client gets `formatted`/`majorUnits` it can render without doing money math; the
server can change `Invoice` internals without breaking the API.
**Failure modes:** the boundary is *inconsistent* — `Invoice` is wrapped in `InvoiceResponse`, but
`Customer`, `Plan`, `Coupon`, `Subscription`, `Payment`, `WebhookEvent` are serialized **raw** in the
[dashboard payload](../../src/Billing.Api/Program.cs#L53-L62). So half the contract is coupled to
entities. And the client *still* re-implements money formatting anyway
([App.tsx formatMoney L394-L396](../../src/Billing.Web/src/App.tsx#L394-L396)), defeating the DTO's purpose.
**Use when:** the API is a stable contract consumed by clients you don't control.
**Avoid when:** purely internal, single-deploy code where the mapping is pure overhead.
**Drill:** which dashboard fields would break the React app if you renamed a C# property? List the
raw-entity ones and propose which deserve a DTO.

---

## Pattern: In-memory store behind a single lock (coarse-grained concurrency)

**Problem:** shared mutable state accessed by HTTP threads *and* background workers must not corrupt.
**General shape:** put all state behind one object; guard every read and write with the same lock;
hand out *copies*, never live references.

**Real example:** [`BillingStore`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5) — one
`_gate` ([L7](../../src/Billing.Api/Infrastructure/BillingStore.cs#L7)), every method `lock`s it, and
list accessors return `.ToArray()` snapshots ([e.g. L26](../../src/Billing.Api/Infrastructure/BillingStore.cs#L26)).
**Second example:** [`IdempotencyStore`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L90-L120)
uses the same coarse-lock shape.

**Why it works:** it's *correct* and trivial to reason about — there's exactly one lock, so no
deadlock ordering to worry about, and snapshots mean callers can't mutate internal collections.
**Failure modes:** it's a single point of contention (every dashboard load takes the lock 8 times);
the snapshots are O(n) copies; and "transaction" semantics across multiple `Save*` calls don't exist
— the [3 writes in GenerateInvoice](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L88) are
*not* atomic with each other (each takes and releases the lock).
**Use when:** prototypes, caches, single-process tools where a DB is overkill.
**Avoid when:** you need durability, multi-process scale, or real transactions — see
[Comparison 1](07-comparative-implementations.md).
**Drill:** find a multi-step operation that would be wrong if it interleaved with another thread
between its `Save*` calls. (Hint: invoice generation marks usage billed and advances the period in
separate locked calls.)

---

## Pattern: Idempotent receiver (cross-cutting middleware)

**Problem:** networks retry; a retried "create payment" must not double-charge.
**General shape:** the client sends an idempotency key; the server caches the first response per key
and replays it on repeats, rejecting key reuse with a different body.

**Real example:** [`IdempotencyMiddleware`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9-L68)
— hashes method+path+body, replays cached 2xx, returns `409` on body mismatch
([L37-L42](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L37-L42)).
**Second example:** the client side — [`postJson` auto-generates a key](../../src/Billing.Web/src/api.ts#L149-L158).

**Why it works:** it's a clean cross-cutting concern — endpoints know nothing about it; it applies
uniformly to all `/api` mutations ([RequiresIdempotency L70-L72](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L70-L72)).
**Failure modes:** (1) the cache and the business write aren't transactional — concurrent duplicates
can both miss; (2) the cache is in-memory, so a restart loses keys; (3) missing key = no protection,
silently ([L17-L21](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L17-L21)). See
[Comparison 3 placement discussion](07-comparative-implementations.md) and [R-04](../08-reference/risk-register.md).
**Use when:** unsafe operations over an unreliable transport (payments, orders).
**Avoid when:** naturally idempotent operations (a pure PUT-by-id) — the key adds nothing.
**Drill:** sketch the smallest change that closes the concurrent-duplicate window. (Per-key lock? A
"reserved" marker written *before* `next()`?)

---

## Pattern: Background worker draining a queue (producer/consumer)

**Problem:** some work shouldn't block the HTTP response (charging a card, sending a webhook).
**General shape:** producers enqueue; a long-running consumer polls the queue on a timer and processes
due items.

**Real example:** [`PaymentWorker`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L46) and
[`WebhookDeliveryWorker`](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6-L55) —
`BackgroundService` + `PeriodicTimer` ([L16](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L16)),
draining via `TryDequeue*` until empty.
**Second example:** the two queues in the store —
[`_paymentJobs`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L15) and
[`_webhookQueue`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L17).

**Why it works:** the HTTP handler returns `202` immediately ([pay endpoint L215](../../src/Billing.Api/Program.cs#L215));
the worker uses `PeriodicTimer` (the modern, allocation-light timer) and honors the
`CancellationToken` for clean shutdown ([L18](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L18)).
**Failure modes:** the queue is in-memory (lost on restart → lost jobs); each tick drains the *entire*
queue synchronously, so a flood starves the timer; and `AvailableAt`-based delay is honored only at
the front of a FIFO queue ([TryDequeuePaymentJob L204](../../src/Billing.Api/Infrastructure/BillingStore.cs#L204)),
so one not-yet-due job blocks everything behind it.
**Use when:** deferrable, retriable side effects.
**Avoid when:** the caller needs the result synchronously, or you need durability without a real
queue.
**Drill:** the payment queue is FIFO and gated by `AvailableAt`. Construct an ordering where a
ready job waits behind a delayed one. Why does the webhook queue avoid this?
(See [TryDequeueWebhook L229-L255](../../src/Billing.Api/Infrastructure/BillingStore.cs#L229-L255).)

---

## Pattern: Transactional outbox (simulated)

**Problem:** if you do a DB write *and* send a webhook, a crash between them loses one or the other.
**General shape:** write the "to-send" message to storage in the *same* transaction as the business
change; a separate worker delivers it later and marks it done.

**Real example:** the engine enqueues a `WebhookEvent` row right after saving the invoice
([GenerateInvoice L100-L110](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L100-L110),
[ApplyPaymentResult L181-L199](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L181-L199)); the
[`WebhookDeliveryWorker`](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L26-L54) drains
and marks `Succeeded`.
**Second example:** none — payments use a job queue, not an outbox row.

**Why it works:** it's the *right shape* for at-least-once delivery and survives the worker being
slow. The retry/backoff code ([L41-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L41-L53))
shows the intended exponential-backoff design.
**Failure modes:** here it's only *simulated* — there's no real HTTP call, so it always "succeeds" on
the first try, which makes the **entire catch/backoff/requeue branch dead code** (the `try` body
can't throw). Worse, `LastError` is set to a note on the *success* path
([L35](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L35)) — a semantic bug. And the
enqueue isn't transactional with the invoice save (separate locked calls). See
[R-06](../08-reference/risk-register.md).
**Use when:** you must reliably emit events alongside DB writes.
**Avoid when:** the event is best-effort and loss is acceptable (then just fire-and-forget).
**Drill:** make the retry path *reachable*: where would a real HTTP delivery go, and what's the
smallest seam (an injected `IWebhookSender`) that lets a test force a failure and assert backoff?

---

## Pattern: Options pattern (typed configuration)

**Problem:** reading config by string key everywhere is fragile and untestable.
**General shape:** bind a config section to a strongly-typed class with defaults; inject `IOptions<T>`.

**Real example:** [`BillingOptions`](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L3-L9)
bound from the `Billing` section ([Program.cs L9](../../src/Billing.Api/Program.cs#L9)), consumed via
`IOptions<BillingOptions>` in the [middleware](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9),
[workers](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L10), and
[verifier](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L7).
**Second example:** the defaults baked into the class ([L5-L8](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L5-L8))
mean it works with no config at all.

**Why it works:** one typed surface for every knob; defaults live with the type; testable by
constructing `Options.Create(new BillingOptions{…})`.
**Failure modes:** `IOptions<T>` is a *singleton snapshot* — config changes need `IOptionsMonitor<T>`
to hot-reload; and `MinValue` guards live at call sites (`Math.Max(1, …)`
[PaymentWorker L15](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15)) rather than being
validated once on bind.
**Use when:** any non-trivial configuration.
**Avoid when:** a single constant that never varies by environment.
**Drill:** add `[Required]`/range validation so an invalid `IdempotencyTtlHours` fails *at startup*
instead of being silently `Math.Max`'d. Which `ValidateOnStart` call do you add in `Program.cs`?

## Verification Notes

Every "real example" anchor was opened and read on 2026-06-09. "Failure mode" claims that assert a
runtime behavior (dead retry path, concurrent-duplicate window) are read-verified from the control
flow, not executed; the riskier ones are cross-referenced to the [risk register](../08-reference/risk-register.md).
