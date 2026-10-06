# Architecture

> **Status (2026-10-06):** this is the original design note. Two things it doesn't say: `main` doesn't compile as
> committed (two one-line errors; see `training/_answers/review.md` finding 0 or `docs2/00-fast-track.md` step 1),
> and there are no tests and no CI workflow. The idempotency section is accurate but incomplete: only 2xx
> responses are cached (`IdempotencyMiddleware.cs:63-67`), requests *without* an `Idempotency-Key` header bypass the
> middleware entirely (lines 17-21), and two concurrent first requests with the same key both run the handler. The
> store is checked at line 35, but nothing is reserved until line 66.


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

## Background Jobs

`PaymentWorker` consumes queued payment jobs and simulates provider success.

`WebhookDeliveryWorker` consumes queued outbound webhook events and marks delivery as simulated. This mirrors an outbox pattern without requiring network calls during local learning.

