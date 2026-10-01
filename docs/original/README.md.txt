# Subscription Billing Dashboard

A learning-oriented Stripe Billing / Chargebee style dashboard built with C#, .NET minimal APIs, and React.

The implementation focuses on enterprise billing fundamentals:

- Customer, plan, subscription, coupon, invoice, usage event, and webhook domain models.
- Money represented as integer minor units to avoid floating point drift.
- Idempotent write APIs via `Idempotency-Key`.
- Background processing for queued payments and outbound webhook dispatch.
- Signed inbound webhook endpoint using HMAC SHA-256.
- Usage-based billing simulator with metered plans.

## Prerequisites

This workspace was generated without installing anything. To run it elsewhere, install the normal toolchains:

- .NET SDK 8 or later
- Node.js 20 or later

## Run

Backend:

```powershell
dotnet run --project src/Billing.Api
```

Frontend:

```powershell
cd src/Billing.Web
npm install
npm run dev
```

The React app expects the API at `http://localhost:5088` by default. Override with `VITE_API_BASE_URL`.

## API Highlights

- `GET /api/dashboard`
- `GET /api/customers`
- `POST /api/customers`
- `GET /api/plans`
- `POST /api/plans`
- `GET /api/invoices`
- `POST /api/subscriptions`
- `POST /api/usage-events`
- `POST /api/simulator/run-cycle`
- `POST /api/invoices/{invoiceId}/pay`
- `POST /api/webhooks/payment-provider`

Use `Idempotency-Key` on unsafe methods that create or mutate billing state.

## Notes

The backend uses an in-memory store so the project stays dependency-free and easy to study. In a production system, the store boundary is where a relational database, durable job queue, distributed idempotency table, and transactional outbox would be added.

More design notes:

- [Architecture](docs/architecture.md)
- [Webhook design](docs/webhooks.md)

## Local Verification

This workspace did not have `dotnet`, `csc`, `msbuild`, `tsc`, or `vite` available on PATH, and frontend packages were not installed. JSON files were validated with Node. Full compilation should be run after installing the normal .NET and Node toolchains on the target machine.
