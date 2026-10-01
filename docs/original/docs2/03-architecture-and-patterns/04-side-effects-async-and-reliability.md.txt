# 03.04 — Side Effects, Async & Reliability

Billing is mostly side effects: charge a card, advance a period, emit a webhook. This page maps how
this repo defers and retries work, and where its reliability story is solid vs theatrical.

## The async machinery

| Mechanism | Where | Shape |
| --- | --- | --- |
| Job queue | [`_paymentJobs`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L15) drained by [`PaymentWorker`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6) | FIFO with `AvailableAt` delay |
| Outbox | [`_webhookEvents` + `_webhookQueue`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L16-L17) drained by [`WebhookDeliveryWorker`](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6) | dict + id-queue, status per row |
| Timer loop | `PeriodicTimer` in both workers | tick → drain-all → wait |
| Cancellation | `stoppingToken` honored in both `while` loops ([PaymentWorker L18](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L18)) | clean shutdown |

The async *style* is good: modern `PeriodicTimer`, proper `CancellationToken` propagation, `async`
all the way (no `.Result`/`.Wait()` sync-over-async anywhere — checked across all files; **none
found**, which is itself worth noting — sync-over-async deadlocks are a classic ASP.NET bug this repo
avoids).

## Idempotency, retries, outbox, timeouts — scored honestly

| Reliability property | Status here | Evidence |
| --- | --- | --- |
| **Idempotent writes (HTTP)** | Partial | Middleware replays by key, but no key ⇒ no protection ([L17-L21](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L17-L21)); concurrent-duplicate race (R-04). |
| **Idempotent processing (engine)** | Missing | `ApplyPaymentResult` writes a new payment each call (R-05). |
| **Retries** | Theatrical | Backoff code exists but is unreachable — the delivery can't fail (R-06). |
| **Outbox** | Right shape, simulated | Rows enqueued + drained, but no real send, not transactional with the write. |
| **Timeouts** | Missing | No HTTP client, no `HttpClient` timeout, no `CancellationToken` on the (simulated) send. |
| **Failure visibility** | Weak | `LastError` is set on *success* ([L35](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L35)); failures only logged, no metrics/alerts. |
| **At-least-once delivery** | Yes, trivially | Always "succeeds" once; the harder at-least-once-under-failure case is never exercised. |

## The payment job lifecycle

```
POST /pay → QueuePayment: write Payment(Pending) + enqueue PaymentJob(AvailableAt=now)   [202 Accepted]
                                                   │
PaymentWorker tick (4s) → TryDequeuePaymentJob(now) when AvailableAt ≤ now
                                                   │
            Process: invoice missing? drop+log │ already paid? skip │ else ApplyPaymentResult(success)
                                                   │
ApplyPaymentResult → invoice=Paid + Payment(Succeeded) + enqueue WebhookEvent
                                                   │
WebhookDeliveryWorker tick (8s) → mark Succeeded
```

Two correctness guards worth noting: the worker **drops** jobs for vanished invoices with a log
([L29-L33](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L29-L33)) and **skips** already-paid
invoices ([L35-L38](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L35-L38)). The skip is the
one thing standing between the system and a double-charge when the inbound webhook beats the worker.

## The FIFO-delay trap (a real subtlety)

The payment queue is a plain `Queue<PaymentJob>`; `TryDequeuePaymentJob` only dequeues if the **front**
job's `AvailableAt ≤ now` ([L204](../../src/Billing.Api/Infrastructure/BillingStore.cs#L204)). So one
not-yet-due job at the front **blocks every ready job behind it**. Today every job is available
immediately ([`EnqueuePaymentJob(invoice.Id, clock.UtcNow)`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L153)),
so the trap is dormant — but the moment you add a retry delay it bites. Contrast the webhook dequeue,
which **skips** not-yet-due rows by walking the queue
([TryDequeueWebhook L233-L250](../../src/Billing.Api/Infrastructure/BillingStore.cs#L233-L250)) — a
better design that the payment queue doesn't share.

## Where idempotency *should* live

The middleware approach ([IdempotencyMiddleware](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs))
is a clean cross-cutting concern, but it caches the *HTTP response* separately from the *business
write*. The two failure modes that creates:

1. **Race (R-04):** miss → execute → miss → execute, for concurrent same-key requests.
2. **Restart amnesia:** the cache is in-memory; a restart loses keys, so a retry after restart
   re-executes.

The production answer is to make idempotency *part of the transaction*: write the idempotency key in
the same DB transaction as the mutation, with a unique constraint, so a duplicate key fails the write
atomically. That's the same lesson as the [outbox](05-pattern-catalog.md) — reliability comes from
*atomicity with the business change*, not from a bolt-on cache.

## Failure-visibility drill

You're on call. A webhook to a customer's endpoint starts failing in production (once real HTTP
exists). Walk the code: how would you find out? **Self-grade — Basic:** you find the
`logger.LogWarning` in the catch ([L42](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L42)).
**Solid:** you note there are no metrics/health signals, so you'd only know from log spelunking.
**Strong:** you note that *today the catch is unreachable*, so even that log never fires — and you
specify the minimal observability (a delivered/failed counter + a "rows stuck Pending > N min" alert)
you'd add. See [05/06](../05-quality-engineering/06-observability-and-operations.md).

## Verification Notes

Queue/worker mechanics, the FIFO-delay trap, and the "no sync-over-async" observation are
read-verified on 2026-06-09 across all infrastructure files. Reliability scoring reflects code paths,
not runtime measurement. R-04/R-05/R-06 cross-reference the [risk register](../08-reference/risk-register.md).
