# .NET Runtime Model

## Concept: CLR, JIT, And Allocation

**Why it matters:** Hosted services and API requests run in the same process, so CPU, memory, and blocking behavior share one runtime budget. The app registers the API services and two hosted workers in [Program.cs](../../../src/Billing.Api/Program.cs#L14-L20).

**Real code:** `Money` is a `readonly record struct` in [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57). Records such as `Invoice` are reference types in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L78-L92). Store read methods allocate arrays with `.ToArray()` in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62).

**Failure modes:** unbounded dashboard reads can allocate large arrays; see aggregate endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63). Worker loops that do too much CPU can affect request latency because workers are hosted in the same process in [Program.cs](../../../src/Billing.Api/Program.cs#L19-L20).

**Drill:** For [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62), mark every method that copies data and explain why copying helps isolation but can hurt memory.

## Concept: async/await And Timers

**Why it matters:** Async is for freeing threads during waits, not making CPU work disappear. Workers use `PeriodicTimer` in [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L24) and [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L15-L24).

**Real code:** API webhook handler asynchronously reads the body in [Program.cs](../../../src/Billing.Api/Program.cs#L223-L228). Idempotency middleware asynchronously reads and replays bodies in [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9-L88).

**Failure modes:** missing cancellation propagation appears in endpoint handlers such as [Program.cs](../../../src/Billing.Api/Program.cs#L204-L209); investigate adding `CancellationToken` for expensive future work. Sync-over-async deadlocks are less likely in ASP.NET Core than legacy ASP.NET, but blocking calls inside workers would still burn thread-pool capacity.

**Drill:** Add comments on where cancellation would enter the run-cycle path: [App.tsx](../../../src/Billing.Web/src/App.tsx#L145-L149), [Program.cs](../../../src/Billing.Api/Program.cs#L204-L209), [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124).

## Concept: Time And Determinism

**Why it matters:** Billing logic depends on time, and tests need deterministic clocks. The repo has `IBillingClock` in [BillingClock.cs](../../../src/Billing.Api/Domain/BillingClock.cs#L3-L10), injected into the store and engine in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L19-L22) and [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7-L7).

**Failure modes:** `WebhookSignatureVerifier` uses `DateTimeOffset.UtcNow` directly in [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L23-L26), making clock-skew tests harder. Senior improvement: inject `IBillingClock` there too.

## Concept: IDisposable/using

**Real code:** The webhook handler wraps `StreamReader` with `using` in [Program.cs](../../../src/Billing.Api/Program.cs#L225-L228). Workers wrap `PeriodicTimer` with `using var` in [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L16).

**Drill:** Explain why disposing the timer matters when the host stops.

## Not Present: ValueTask, Span, ConfigureAwait

No `ValueTask`, `Span<T>`, or `ConfigureAwait` usage was found. Do not add them as cleverness. Reach for them only after measurement or library-boundary need. Anchor current async style to [Program.cs](../../../src/Billing.Api/Program.cs#L223-L250) and [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9-L88).

