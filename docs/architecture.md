# Architecture

## Backend

`Billing.Api` is a dependency-free .NET 8 minimal API.

- `Domain` contains immutable billing models and `Money`.
- `Contracts` contains request and response DTOs.
- `Infrastructure` contains in-memory persistence, idempotency, billing cycle logic, payment processing, and webhook delivery.
- `Program.cs` wires HTTP endpoints and application services.

The store is intentionally in-memory for learning. A production migration should replace it with:

- Relational tables for customers, plans, subscriptions, usage events, invoices, invoice lines, payments, coupons, idempotency keys, and webhook events.
- A transactional outbox for webhook delivery.
- A durable queue for payment and invoice jobs.
- Tenant scoping and role-based authorization.
- A proper reporting currency or FX conversion layer for multi-currency metrics.

## Money

Money is represented as integer minor units plus ISO currency. This avoids binary floating point drift and makes invoice totals deterministic. The sample app supports cent-level pricing; sub-cent metered pricing would need a separate price type such as `UnitPriceMicros` and a final invoice rounding policy.

## Idempotency

Unsafe `/api` methods accept `Idempotency-Key`. The middleware hashes method, path, and body. A repeated key with the same request body replays the first successful response. A repeated key with a different body returns `409 Conflict`.

In production, store idempotency keys in the same database boundary as the business mutation or use a transactionally consistent write path.

## Background jobs (there are none)

There is no hosted background service in this build. Both queues are drained by an
explicit request: `POST /api/simulator/process`, handled by `BillingEngine.Process`.

That call completes up to `BillingLimits.PaymentBatchSize` (100) due payment jobs with the
success or decline outcome the caller chose, and marks up to `BillingLimits.WebhookBatchSize`
(200) pending outbox events as `Simulated` in the same transaction. No network request is
made; the response reports `externalRequests = 0`. This mirrors an outbox pattern without
requiring network calls during local learning, and it makes the intermediate `Pending` state
inspectable, which a timer-driven worker would not.

The original hosted `PaymentWorker` and `WebhookDeliveryWorker` are preserved verbatim under
`docs/original` for comparison.

