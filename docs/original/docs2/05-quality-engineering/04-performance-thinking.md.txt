# 05.04 — Performance Thinking

Rule zero: **measure before you optimize.** This repo has no profiler/benchmark setup, so everything
below is a *read-identified* hotspot — a hypothesis to confirm with a measurement, not a confirmed
bottleneck. The value is learning to *spot the shapes*; the discipline is to verify before acting.

## The shapes that matter, located here

| Shape | Where | Today's impact | Why it matters at scale |
| --- | --- | --- | --- |
| **Repeated full scans** | [`GetUnbilledUsage`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L182-L188) scans all usage; uniqueness checks scan all rows ([L68](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68), [L82](../../src/Billing.Api/Infrastructure/BillingStore.cs#L82)) | fine for hundreds of rows | O(n) per call → table scans without an index in a DB |
| **N+1** | [`RunBillingCycle`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L118-L124) calls a per-subscription usage query in a loop | fine in memory | a round-trip per subscription against a DB |
| **O(n) snapshot copies** | every list accessor `.ToArray()` under the lock ([e.g. L51](../../src/Billing.Api/Infrastructure/BillingStore.cs#L51)) | cheap now | the dashboard copies 8 collections per request |
| **Lock contention** | one global `_gate` ([L7](../../src/Billing.Api/Infrastructure/BillingStore.cs#L7)) serializes *all* reads and writes | none at low load | a single-writer ceiling; the dashboard takes the lock 8× |
| **Unbounded inputs** | usage `Quantity` has no upper bound ([L180](../../src/Billing.Api/Program.cs#L180)); no pagination on any list endpoint | none now | `GET /api/invoices` returns *all* invoices, forever |
| **Whole-payload refetch** | the SPA refetches the entire dashboard after every write ([App.tsx L77-L78](../../src/Billing.Web/src/App.tsx#L77-L78)) | trivial now | grows with total data, not with what changed |
| **Response buffering** | idempotency mw buffers the full response in a `MemoryStream` ([L52-L60](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L52-L60)) | fine for small JSON | a large response doubles memory per request |

## The dashboard, costed

`GET /api/dashboard` ([Program.cs L41-L63](../../src/Billing.Api/Program.cs#L41-L63)) is the busiest
endpoint and the one to watch:

- It calls **8 store accessors** ([L43-L50](../../src/Billing.Api/Program.cs#L43-L50)), each taking the
  lock and producing a sorted `.ToArray()` copy.
- `CalculateMetrics` then re-iterates customers, subscriptions, invoices, and builds a plan lookup
  dictionary ([L261](../../src/Billing.Api/Program.cs#L261)).
- It maps every invoice to a DTO with nested line DTOs ([L58](../../src/Billing.Api/Program.cs#L58)).

At seed scale (2 customers, a handful of invoices) this is nothing. At 100k invoices it's: lock held
across a 100k-row sort + copy, ×8, plus a full re-scan for metrics, every refresh. **This is the first
thing to paginate and to move off the global lock when this becomes a DB.**

## Serial awaits / async

There's no obvious serial-await waterfall (the endpoints are mostly synchronous over the in-memory
store). The async paths are the workers, and they're correctly non-blocking. The one thing to watch:
each worker tick **drains the whole queue synchronously**
([PaymentWorker L20-L23](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L20-L23)) — under real
I/O that would serialize all deliveries on one thread per tick.

## Money math

`Money` operations allocate nothing (it's a struct) and use integer/decimal arithmetic — cheap and
correct. `CalculateMetrics`'s `Aggregate` over subscriptions ([L262-L274](../../src/Billing.Api/Program.cs#L262-L274))
is O(active subs), fine. No perf concern; the concern there is correctness (R-01), not speed.

## How you'd actually measure (when you can run it)

1. **Load the dashboard under `dotnet-counters` / a stopwatch** with N seeded invoices; find where time
   goes before touching anything.
2. **For the DB future:** turn on EF query logging and watch for the `RunBillingCycle` N+1 — count the
   queries per cycle run.
3. **Front end:** the network panel — is the dashboard payload size growing linearly with data?

## The senior discipline

The temptation is to "fix" the linear scans now. Don't — at this scale they're free, and the in-memory
store is slated for replacement anyway. The *right* performance work here is to (a) add pagination to
list endpoints before data grows, and (b) carry the index/query knowledge into the
[EF migration](../03-architecture-and-patterns/07-comparative-implementations.md) so the hotspots are
indexed from day one. Optimizing the in-memory version is effort spent on code that's leaving.

## Drill

Estimate the cost of `GET /api/dashboard` at 100k invoices and 1M usage events. Which line dominates?
**Self-grade — Basic:** you name the invoice sort/copy. **Solid:** you note the lock is held across all
8 copies, serializing every concurrent request. **Strong:** you prioritize pagination + the DB indexes
over micro-optimizing the scans, and justify it by "this code is being replaced."

## Verification Notes

Every hotspot is read-verified from the anchored code on 2026-06-09. **No measurements were taken** —
there is no profiler setup and no .NET runtime here, so all impact claims are read-identified
hypotheses. Confirm with a real measurement before optimizing anything.
