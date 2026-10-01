> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/01-codebase-cartography/01-system-map.md.txt).

# 01.01 — System Map

## The project graph

```
SubscriptionBillingDashboard.sln          (contains ONLY Billing.Api — the web app is on disk, not in the .sln)
│
├── src/Billing.Api          .NET 8 minimal API (net8.0, Microsoft.NET.Sdk.Web)   ← all server logic
│   ├── Program.cs           Composition root: DI registration + every HTTP endpoint
│   ├── Domain/              Pure model: records, enums, Money, the clock interface
│   ├── Contracts/           Request + response DTOs (the wire shape)
│   └── Infrastructure/      The "guts": store, engine, idempotency, workers, webhook verify, options
│
└── src/Billing.Web          React 19 + Vite 6 SPA (TypeScript)                    ← operator dashboard
    ├── index.html           Mounts #root
    └── src/{main,App,api}.tsx/ts + styles.css
```

There is **no test project**, no shared library, no `Directory.Build.props`, no `global.json`, no
Docker/compose. It is two folders. (Verified by full file listing; see
[verification-log](../08-reference/verification-log.md).)

## Ownership: who owns what (and what they must NOT own)

| Layer | Lives in | Owns | Must NOT own |
| --- | --- | --- | --- |
| **UI** | [src/Billing.Web/src/App.tsx](../../src/Billing.Web/src/App.tsx) | Rendering, form state, calling the API, formatting for display | Money math, authorization, business rules |
| **Client contract** | [api.ts](../../src/Billing.Web/src/api.ts) | TS mirrors of the C# DTOs, fetch wrapper, idempotency-key generation | Domain logic |
| **HTTP surface** | [Program.cs](../../src/Billing.Api/Program.cs#L37-L250) | Routing, request validation, mapping exceptions → status codes | Persistence details, money math, period math |
| **Cross-cutting** | [IdempotencyMiddleware](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7), [WebhookSignatureVerifier](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L7) | Replay protection, signature checks | Knowing what an invoice *is* |
| **Business logic** | [BillingEngine](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7) | Invoice generation, discounting, payment application, period advance, webhook enqueue | HTTP concerns, storage mechanics |
| **Domain model** | [Domain/](../../src/Billing.Api/Domain) | Invariants of `Money`, the shape of every entity, the `Coupon` discount rule | Persistence, HTTP, time (it asks `IBillingClock`) |
| **Persistence** | [BillingStore](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5) | All state, thread-safe access, uniqueness checks, the two queues, seed data | Business rules (it stores, it doesn't decide) |
| **Background work** | [PaymentWorker](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6), [WebhookDeliveryWorker](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6) | Draining queues on a timer, calling the engine | Defining what success means (they delegate) |

### Where that ownership leaks (note these now, study them in module 03)

- **Money math leaks into the UI**: [App.tsx `formatMoney`](../../src/Billing.Web/src/App.tsx#L394-L396)
  divides `minorUnits / 100` itself instead of using the server-provided `formatted`/`majorUnits`
  fields. Two formatters now exist (server `Money.ToString()` and client `Intl.NumberFormat`).
- **The dashboard endpoint owns metric math** inline in `Program.cs`
  ([CalculateMetrics](../../src/Billing.Api/Program.cs#L254-L290)) rather than in the engine — so a
  currency assumption ("USD") sits in the HTTP layer. See [risk-register R-01](../08-reference/risk-register.md).

## Request topology

```
Browser (React SPA, :5173)
   │  fetch() with Idempotency-Key on every POST  (api.ts L149-L158)
   ▼
CORS policy "dashboard"  ──────────────  Program.cs L21-L30, L34   (only :5173 origins)
   ▼
IdempotencyMiddleware  ───────────────  Program.cs L35            (replay / 409 / pass-through)
   ▼
MapGroup("/api") endpoints  ──────────  Program.cs L37-L250
   ▼
BillingEngine  ◄──►  BillingStore (in-memory, single lock)
                         ▲
                         │ enqueue
            ┌────────────┴───────────────┐
   PaymentWorker (4s timer)      WebhookDeliveryWorker (8s timer)   ← BackgroundService loops
```

Inbound provider webhooks enter through the same `/api` group but bypass the *business* idempotency
by being a fresh POST; they are gated by HMAC signature instead
([Program.cs L223-L250](../../src/Billing.Api/Program.cs#L223-L250)).

## Public vs private surface

**Public (HTTP, anonymous — there is no auth):** every route under `/api`
([Program.cs L37-L250](../../src/Billing.Api/Program.cs#L37-L250)) —
`health, dashboard, customers (GET/POST), plans (GET/POST), coupons (GET/POST), subscriptions
(GET/POST), invoices (GET), usage-events (POST), simulator/run-cycle (POST),
invoices/{id}/pay (POST), webhooks/payment-provider (POST)`.

**Effectively internal (not exposed but reachable through the engine):**
`GenerateInvoice`, `ApplyPaymentResult` (only the engine calls them; `pay` and the inbound webhook
trigger them).

**Private:** everything in `BillingStore` (only the engine/endpoints call it), the workers (only the
host starts them), `Money` internals.

> **The senior read of this map:** every public endpoint is anonymous and operates on a *global*
> dataset — there is no notion of "my customers". The single most consequential thing missing from
> this system map is an **authorization boundary** between the HTTP surface and the store. Hold that
> for [03/03](../03-architecture-and-patterns/03-validation-auth-and-permissions.md).

## Verification Notes

Read-verified from the full file listing and each source file on 2026-06-09. Nothing here was
executed. The "leaks" are read-verified code observations, not runtime bugs unless cross-referenced
to the risk register.
