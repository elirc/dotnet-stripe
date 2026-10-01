> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/07-career-and-collaboration/04-interview-prep-from-this-repo.md.txt).

# 07.04 — Interview Prep From This Repo

Mid-level interviews are won on *judgment shown through concrete examples*, not trivia. This repo is
your example bank. Every question below is grounded in real code here, and each has a **junior /
mid-level / senior** answer sketch so you can hear the ladder. Practice out loud, pointing at anchors.

> **The format that scores:** *claim → concrete example from this repo → trade-off → what you'd do.*
> "Money is `decimal`? No — here it's integer minor units in [Money.cs](../../src/Billing.Api/Domain/Money.cs#L7),
> because summing floats drifts; the cost is a 2-decimal assumption I'd fix with a per-currency exponent."

## A. C#/.NET runtime

**A1. `record` vs `class` vs `struct` — and why is `Money` a `readonly record struct`?**
- *Junior:* records are immutable value-ish types with value equality; `Money` uses one.
- *Mid:* [`Money`](../../src/Billing.Api/Domain/Money.cs#L5) is a `readonly record struct` → value
  semantics (two `Money`s with the same minor units + currency are equal) **and** no heap allocation
  per value (it's a struct). Entities like [`Invoice`](../../src/Billing.Api/Domain/BillingModels.cs#L78)
  are `sealed record` *classes* — reference types with value equality, fine because they're larger and
  passed around.
- *Senior:* the struct choice matters because `Money` is created in tight loops (every invoice line,
  every aggregate in [`CalculateMetrics`](../../src/Billing.Api/Program.cs#L262-L274)); a class would
  allocate garbage per operation. The trade-off: structs copy by value, so a large struct would be
  worse than a class — `Money` is two fields, so it's the right size for a struct.

**A2. What does `async`/`await` actually do, and where is it used here?**
- *Junior:* it lets the thread do other work while waiting; the workers use it.
- *Mid:* `await` yields the thread back to the pool until the awaited task completes. The
  [`PaymentWorker` loop](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L18) awaits
  `timer.WaitForNextTickAsync`, so it's not burning a thread between ticks. The webhook endpoint
  awaits the body read ([Program.cs L227](../../src/Billing.Api/Program.cs#L227)).
- *Senior:* note what this repo *avoids* — there's no `.Result`/`.Wait()` anywhere, so no
  sync-over-async deadlock (the classic ASP.NET footgun where blocking on a task starves the thread
  pool). I'd point out the workers drain the whole queue *synchronously* inside each async tick, which
  is fine for in-memory work but would block the tick under real I/O.

**A3. `Task` vs `ValueTask`; when would this repo use `ValueTask`?**
- *Junior:* both represent async work; `ValueTask` avoids an allocation.
- *Mid:* `ValueTask` is a struct, good for hot paths that often complete synchronously (cache hits).
  None is used here, but `IdempotencyStore.TryGet` is a synchronous cache lookup — if it were async,
  `ValueTask` would fit.
- *Senior:* `ValueTask` has sharp edges (can't await twice, shouldn't be stored), so I'd only reach
  for it with a measured allocation problem — which this repo doesn't have.

**A4. `IDisposable` / `using` — find one and explain it.**
- *Mid:* [`PaymentWorker`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L16) does
  `using var timer = new PeriodicTimer(...)` so the timer is disposed when `ExecuteAsync` exits on
  shutdown; the idempotency middleware uses `await using var buffer = new MemoryStream()`
  ([L52](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L52)) to free the response
  buffer.
- *Senior:* the `await using` matters — disposing a stream may flush asynchronously; using the async
  form avoids blocking on dispose.

## B. ASP.NET Core / EF Core / React

**B1. Walk the ASP.NET request pipeline in this app.**
- *Mid:* request → CORS ([L34](../../src/Billing.Api/Program.cs#L34)) → `IdempotencyMiddleware`
  ([L35](../../src/Billing.Api/Program.cs#L35)) → routing → the endpoint lambda. Order matters: CORS
  before idempotency, idempotency before the handler so it can replay without executing.
- *Senior:* I'd flag what's *missing* from the pipeline — no `UseExceptionHandler`, so an unhandled
  engine exception (like the currency mismatch in R-01) becomes a raw 500; no auth middleware at all.

**B2. DI lifetimes — everything here is a singleton. When is that a bug?**
- *Junior:* singletons are created once and shared.
- *Mid:* [`BillingStore`, `BillingEngine`, `IdempotencyStore` are singletons](../../src/Billing.Api/Program.cs#L14-L18)
  — correct, because the store *is* the shared in-memory state and must be one instance. The workers
  are `AddHostedService` (also singleton).
- *Senior:* the **captive dependency** trap — if this migrates to EF Core, `DbContext` is *scoped*;
  injecting it into the singleton `BillingEngine` would capture one context forever and corrupt
  change-tracking across requests. The fix is to make the engine scoped or have workers open a scope
  per tick. (This is [Comparison 1](../03-architecture-and-patterns/07-comparative-implementations.md),
  migration step 5 — a favorite mid/senior question.)

**B3. (EF Core, hypothetical) This repo has no EF — how would `GetUnbilledUsage` translate, and what's the N+1 risk?**
- *Mid:* [`GetUnbilledUsage`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L182-L188) is a
  `Where + OrderBy`; in EF it's one indexed query. The N+1 risk is in
  [`RunBillingCycle`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L118-L124), which loops
  subscriptions and calls a per-subscription usage query — N+1 if each is a round-trip.
- *Senior:* I'd batch the usage load or project per-subscription, and add the
  `(SubscriptionId, InvoiceId, OccurredAt)` index. Also `IQueryable` vs `IEnumerable`: the in-memory
  `.Where()` runs in C#; in EF it must stay `IQueryable` to translate to SQL or you accidentally pull
  the whole table and filter in memory.

**B4. React: why does `App.tsx` refetch the whole dashboard after every write?**
- *Junior:* to show fresh data.
- *Mid:* [`runAction`](../../src/Billing.Web/src/App.tsx#L73-L84) calls `refresh()` after a successful
  mutation — simple, always-consistent, but it re-pulls everything.
- *Senior:* it's the right call for one screen; it costs over-fetching and has no request dedup (a
  double-click can fire two writes). React Query would cache + dedup + invalidate granularly — see
  [Comparison 3](../03-architecture-and-patterns/07-comparative-implementations.md) — but it's
  unjustified for a single dashboard.

**B5. React: the `useEffect` at [App.tsx L57-L62](../../src/Billing.Web/src/App.tsx#L57-L62) depends on `dashboard`. Stale-closure risk?**
- *Mid:* it sets default form selections from the loaded dashboard; the functional `setState`
  (`current => current || …`) avoids overwriting a user's choice.
- *Senior:* using the updater form is what dodges the stale closure — reading `subscriptionCustomerId`
  directly in the effect would capture an old value. Good instinct to point at.

## C. Debugging

**C1. The dashboard returns 500 in production but works in the demo. First hypothesis?**
- *Mid:* something in the seed (all USD) differs from prod data. The likely culprit is the currency
  aggregation in [`CalculateMetrics`](../../src/Billing.Api/Program.cs#L262-L274) — a non-USD active
  subscription makes `Money.Add` throw (R-01).
- *Senior:* I'd reproduce by creating a EUR plan + active sub, confirm the throw site, and note there's
  no `UseExceptionHandler` so it surfaces as a raw 500. Fix: reporting currency or per-currency
  grouping. (Full walkthrough in [05/03](../05-quality-engineering/03-systematic-debugging.md).)

**C2. A customer was charged twice. Where do you look?**
- *Mid:* the payment paths — [`ApplyPaymentResult`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200)
  writes a new payment each call and isn't idempotent (R-05); a retried provider webhook with no
  `EventId` dedup re-applies.
- *Senior:* I'd check whether the inbound webhook arrived twice within the 5-min tolerance, confirm no
  dedup on `EventId`, and add `(EventId)`/`(invoiceId, providerReference)` dedup at the engine boundary
  plus a regression test.

## D. System design (grounded in this repo)

**D1. Make webhook delivery actually reliable.**
- *Mid:* real HTTP send with retries/backoff; the [outbox shape](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L26-L54)
  is already there but simulated and its retry branch is dead (R-06).
- *Senior:* introduce `IWebhookSender` (so failures are reachable and testable), write the outbox row
  in the same transaction as the business change, add idempotency keys for the receiver, a
  dead-letter after N attempts, and a "stuck Pending" alert. Trade-off: at-least-once delivery means
  receivers must dedup.

**D2. Add multi-tenancy.**
- *Mid:* add `TenantId` to entities, scope every query.
- *Senior:* auth first (identity/tenant from claims, never the body), a non-bypassable scoping filter
  or store-level scope so endpoints can't forget it, 404-not-403 to avoid leaking existence, and a
  backfill migration. The hard part is that authorization is a cross-cutting invariant — per-endpoint
  checks will miss one (R-02, [03/03](../03-architecture-and-patterns/03-validation-auth-and-permissions.md)).

## E. From the comparison cards (defend your design — these dominate mid/senior interviews)

**E1. "You chose an in-memory store over a database here. Defend it."** (Comparison 1)
- *Mid:* it's a learning repo, dependency-free, fast to run.
- *Senior:* right call *for this purpose* and cleanly replaceable behind a seam; I'd switch the moment
  data must survive a restart, multiple replicas serve traffic, or I need real transactions/reporting
  — and I'd migrate with characterization tests + flag-based rollback, watching for the captive-
  dependency lifetime trap.

**E2. "When do minimal APIs stop being the right call?"** (Comparison 2)
- *Mid:* when there are many endpoints or you want per-resource files.
- *Senior:* the early signal is *here* — the same `catch … → BadRequest` and validation boilerplate is
  copy-pasted across handlers ([customers](../../src/Billing.Api/Program.cs#L79-L82),
  [plans](../../src/Billing.Api/Program.cs#L116-L119)). I'd extract an endpoint filter first; if that
  doesn't contain it, move to controllers for testability and organization.

**E3. "Why no React Query?"** (Comparison 3)
- *Mid:* one screen; `useState` + refetch is enough.
- *Senior:* correct now; I'd adopt it when multiple screens share server state, the full-dashboard
  refetch gets slow, or I need request dedup/optimistic updates — and I'd weigh a 5-line in-component
  guard against a whole dependency first.

## F. Behavioral (grounded in plausible contributions here)

**F1. "Tell me about catching a subtle bug."** Use the dead webhook-retry path (R-06): the backoff
code *looks* robust but is unreachable because the `try` body can't throw. Narrate how you'd notice
(reading, not running), confirm (no throwing statement), and fix (an `IWebhookSender` seam).

**F2. "A teammate's PR adds a feature but no tests."** This repo has *zero* tests (R-03). Talk about
how you'd push for a characterization test on the invoice total before merging — kindly, with the
"this protects your change too" framing (see [07/01](01-code-review-mindset.md)).

**F3. "Disagreeing with a senior's design."** Use the currency assumption: the metric math sits in
`Program.cs` with hardcoded "USD". Practice raising it as a *question* ("what happens with a EUR
plan?") rather than an accusation — see [07/03](03-maintainer-communication.md).

## How to drill this

Pick three questions a day, answer **out loud** at all three levels, pointing at the anchor. Record
yourself once a week and grade against [08/learning-rubrics.md](../08-reference/learning-rubrics.md):
are you naming concrete code, stating the trade-off, and saying what you'd *do*? If you only define
the term, you're answering at junior level.

## Verification Notes

Every code reference is read-verified on 2026-06-09. Answer sketches are interview guidance, not
graded keys; the *code facts* inside them are anchored and accurate, the *judgment* is one defensible
take among several.
