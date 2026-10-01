# 02.01 — .NET Runtime Model

The runtime concepts that actually show up in this code, each anchored.

## CLR & JIT (the 30-second model)

C# compiles to IL (intermediate language); at runtime the **CLR** JIT-compiles IL to machine code on
first call. `Program.cs` uses **top-level statements** ([Program.cs L7](../../src/Billing.Api/Program.cs#L7))
— the compiler wraps them in a generated `Main`. `ImplicitUsings` ([csproj L5](../../src/Billing.Api/Billing.Api.csproj#L5))
means common namespaces (`System`, `System.Linq`, …) are imported for you, which is why the files have
so few `using` lines. **Why it matters:** the first request after startup pays a JIT cost; for a
billing job that runs on a timer, that's invisible — but it's why "the first call is slow" is normal.

## Garbage collection & allocations

The CLR's GC reclaims unreferenced heap objects. The relevant skill is *reducing allocations on hot
paths*. This repo's notable choice:

- **`Money` is a `struct`** ([Money L5](../../src/Billing.Api/Domain/Money.cs#L5)), so it lives on the
  stack / inline in its container — **no heap allocation per money value**. In
  [`CalculateMetrics`](../../src/Billing.Api/Program.cs#L262-L274), MRR is summed by repeated
  `Money.Add`; if `Money` were a class, each add would allocate. As a struct, it doesn't.
- **Records are classes** ([Invoice etc.](../../src/Billing.Api/Domain/BillingModels.cs#L78)) — each
  `with` allocates a new object ([e.g. mark-paid L165-L167](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L165-L167)).
  Fine here (low volume), but "immutable update in a tight loop" is an allocation pattern to watch.
- **`.ToArray()` snapshots** in the store ([L26](../../src/Billing.Api/Infrastructure/BillingStore.cs#L26))
  allocate a fresh array per call — cheap now, a cost at scale (see [performance](../05-quality-engineering/04-performance-thinking.md)).

## async / await

`await` releases the thread back to the pool until the awaited operation completes — that's how a few
threads serve many concurrent requests. Real uses here:

- The worker loop awaits the timer: [`await timer.WaitForNextTickAsync(stoppingToken)`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L18)
  — between ticks, *no thread is held*.
- The webhook endpoint awaits the body read: [`await reader.ReadToEndAsync()`](../../src/Billing.Api/Program.cs#L227).

## Task vs ValueTask

- **`Task`** is the default async return type; it's a heap object representing future work.
- **`ValueTask`** is a struct that avoids the allocation when the result is often already available
  (cache hits). It has sharp edges (don't await it twice, don't store it).

This repo uses only `Task`. A place `ValueTask` *could* fit: a synchronous-mostly cache like
[`IdempotencyStore.TryGet`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L95-L102) — but
it's synchronous, so it doesn't even need a task. **Lesson:** reach for `ValueTask` only with a measured
allocation problem; this repo correctly doesn't bother.

## ConfigureAwait & the sync-over-async deadlock

In classic ASP.NET (with a synchronization context), blocking on async (`task.Result` / `task.Wait()`)
can **deadlock** — the awaited continuation needs the thread that's blocked waiting for it. ASP.NET Core
has no sync context, so the risk is lower, but the rule stands: **never block on async.**

**This repo gets it right:** there is *no* `.Result` or `.Wait()` anywhere (checked across all files).
Every async path awaits. `ConfigureAwait(false)` isn't used and isn't needed in app code on ASP.NET
Core. **This is a thing to say in an interview** (see [07/04 A2](../07-career-and-collaboration/04-interview-prep-from-this-repo.md)):
the repo avoids the single most common ASP.NET async bug.

## CancellationToken

Cooperative cancellation: a `CancellationToken` is passed down so long-running work can stop cleanly on
shutdown. Both workers honor `stoppingToken`:
[`while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L18).
When the host shuts down, the token is canceled, `WaitForNextTickAsync` throws/returns, and the loop
exits — no orphaned background thread. **Gap to notice:** the *endpoints* don't take a
`CancellationToken` (they're synchronous over memory), so a client disconnect doesn't cancel work — fine
now, relevant once there's real I/O.

## IDisposable / using

Deterministic cleanup of unmanaged-ish resources (timers, streams). Examples:

- [`using var timer = new PeriodicTimer(...)`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L16)
  — the timer is disposed when `ExecuteAsync` returns on shutdown.
- [`await using var buffer = new MemoryStream()`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L52)
  — the `await using` matters because stream disposal can flush asynchronously.
- [`using var reader = new StreamReader(..., leaveOpen: true)`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L77)
  — `leaveOpen: true` disposes the reader but **not** the underlying request body stream (which the
  pipeline still needs). That flag is a real, easy-to-miss detail.

## Spans

`Span<T>`/`ReadOnlySpan<T>` give allocation-free slices of memory. This repo doesn't use them
explicitly, but you see the *idea* in [`InvoiceNumber`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L205-L206):
`invoiceId.ToString("N")[..8]` slices the first 8 chars. (That slice allocates a substring; a span
would avoid it — irrelevant at this volume, but it's the seam where span optimization would live.)

## Drill

Find every `await` in the repo and classify it: is it awaiting real I/O, a timer, or nothing
expensive? **Self-grade — Basic:** you list the worker timer awaits and the body read. **Solid:** you
note there's no `.Result`/`.Wait()` (no deadlock risk). **Strong:** you explain *why* the body read is
awaited (it's network I/O from the client) but the store calls are synchronous (in-memory), and what
changes when the store becomes a database.

## Verification Notes

Every anchor read-verified on 2026-06-09. The "no `.Result`/`.Wait()`" claim was verified by reading all
`.cs` files. Runtime/JIT/GC behavior is standard CLR knowledge applied to this code, not measured here
(no .NET runtime available).
