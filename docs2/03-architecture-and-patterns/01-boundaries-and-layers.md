> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/03-architecture-and-patterns/01-boundaries-and-layers.md.txt).

# 03.01 — Boundaries & Layers

A boundary is a line across which one side may not reach into the other's internals. Get the
boundaries right and changes stay local; get them wrong and a rename in storage breaks the UI. This
repo has clear *intended* boundaries and a few real leaks.

## The intended layering

```
React SPA  ──►  HTTP endpoints (Program.cs)  ──►  BillingEngine  ──►  BillingStore
   │                  │                              │                    │
 display          routing +                     business rules         state +
 + forms          validation +                  (invoicing,            concurrency
                  exception→status              discount, payment)
   ▲                  ▲                              ▲
   └── Contracts (DTOs) define the wire shape ───────┘
       Domain (records, Money, clock) is the shared vocabulary everything below HTTP uses
```

| Layer | Owns | Must not own | Depends on |
| --- | --- | --- | --- |
| UI ([App.tsx](../../src/Billing.Web/src/App.tsx)) | rendering, form state, calling the API | money math, auth, rules | api.ts |
| Contracts ([Contracts/](../../src/Billing.Api/Contracts)) | the wire shape (requests/responses) | logic, storage | Domain |
| HTTP ([Program.cs](../../src/Billing.Api/Program.cs#L37-L250)) | routing, input validation, exception→HTTP | persistence mechanics, period math | Engine, Store, Contracts |
| Engine ([BillingEngine](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7)) | invoicing, discounting, payment application, period advance | HTTP, JSON, storage mechanics | Store, Domain, clock |
| Domain ([Domain/](../../src/Billing.Api/Domain)) | entity shapes, `Money` invariants, the discount rule | persistence, HTTP, wall-clock time | clock interface only |
| Store ([BillingStore](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5)) | all state, thread safety, uniqueness, queues, seed | business decisions | Domain |

## What's done well

- **The Domain has no upward dependencies.** `Money`, the records, and `IBillingClock` know nothing
  about HTTP or storage. You can lift the entire `Domain/` folder into another app.
- **The engine never touches HTTP.** `GenerateInvoice`/`QueuePayment` take/return domain types and
  throw domain exceptions; the endpoints translate those to status codes
  ([catch-when filters, e.g. L170-L173](../../src/Billing.Api/Program.cs#L170-L173)). That's a clean
  separation of "what went wrong" (engine) from "what HTTP code" (endpoint).
- **The store hands out copies, not live references** ([`.ToArray()` snapshots](../../src/Billing.Api/Infrastructure/BillingStore.cs#L26)).
  Callers can't mutate internal state behind the lock's back.

## The leaks (study these)

1. **Money math leaks into the UI.** The server already provides `formatted`/`majorUnits` on every
   `MoneyDto` ([Responses.cs L5-L9](../../src/Billing.Api/Contracts/Responses.cs#L5-L9)), yet
   [`App.tsx formatMoney`](../../src/Billing.Web/src/App.tsx#L394-L396) re-derives a string from
   `minorUnits / 100`. Now the 2-decimal assumption lives in two places (R-10). *Boundary rule
   violated:* the UI owns money math it shouldn't.
2. **Metric math lives in the HTTP layer.** [`CalculateMetrics`](../../src/Billing.Api/Program.cs#L254-L290)
   is a static function in `Program.cs`, not in the engine — so a business rule (how MRR is computed,
   the "USD" assumption) sits above the engine boundary. That's why R-01 is an HTTP-layer crash, not
   an engine one.
3. **DTO boundary is half-applied.** Only `Invoice` gets a response DTO; the dashboard serializes
   `Customer`, `Plan`, `Coupon`, `Subscription`, `Payment`, `WebhookEvent` as raw entities
   ([L53-L62](../../src/Billing.Api/Program.cs#L53-L62)). Rename a property on any of those and the
   React types ([api.ts](../../src/Billing.Web/src/api.ts#L16-L143)) break silently.
4. **`WebhookEvent.PayloadJson` carries a pre-serialized string across the boundary.** The engine
   builds JSON ([GenerateInvoice L90-L98](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L90-L98))
   and the entity stores it as text — so the *contents* of an outbound webhook are an untyped string
   the rest of the system can't reason about.

## Why boundaries matter here specifically

The whole point of the in-memory store is that it's *replaceable* (the README says so). That only
holds if nothing above it depends on its internals. It mostly doesn't — which is why
[Comparison 1's migration](07-comparative-implementations.md) is *feasible*. The leaks above are the
places that migration will snag: every raw-entity dashboard field and every client-side money
calculation is a spot where "swap the store / change the model" ripples outward.

## Drill

Pick `Plan` and trace its blast radius: list every place a `Plan` field is read across the boundary
(server entity → dashboard JSON → [api.ts `Plan`](../../src/Billing.Web/src/api.ts#L24-L35) → 
[App.tsx plans table](../../src/Billing.Web/src/App.tsx#L310-L333)). Now decide: does `Plan` deserve a
`PlanResponse` DTO? **Self-grade — Basic:** you find the table render. **Solid:** you note that
`basePrice`/`unitPrice` are raw `Money` (no `formatted`), so the UI *must* format them itself.
**Strong:** you argue the DTO/raw split should be consistent — either everything gets a DTO or the
money types carry `formatted` everywhere — and say which you'd choose and why.

## Verification Notes

All anchors read-verified on 2026-06-09. Leaks are read-verified code observations; the runtime
consequence of R-01/R-10 is cross-referenced to the [risk register](../08-reference/risk-register.md),
not executed.
