> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/02-stack-and-language-mastery/02-framework-mental-models.md.txt).

# Framework Mental Models

## ASP.NET Core Pipeline

**Concept:** Requests pass through middleware before endpoints.  
**Why it matters:** Cross-cutting concerns like CORS and idempotency should not be duplicated in every handler.  
**Real code:** CORS is configured in [Program.cs](../../../src/Billing.Api/Program.cs#L21-L30), then applied before idempotency in [Program.cs](../../../src/Billing.Api/Program.cs#L32-L37). The API group starts at [Program.cs](../../../src/Billing.Api/Program.cs#L37-L37).

**Failure modes:** if idempotency ran before body buffering was safe, replay logic could break model binding; current middleware reads and resets the request body in [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L73-L81). If auth is added later, decide whether idempotency keys are scoped by user/tenant; current cache key uses method/path/key in [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L40-L45).

## Dependency Injection Lifetimes

**Concept:** Singleton services live for process lifetime.  
**Real code:** `BillingStore`, `BillingEngine`, `IdempotencyStore`, and `WebhookSignatureVerifier` are singletons in [Program.cs](../../../src/Billing.Api/Program.cs#L14-L18). Hosted services consume those singletons from [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L11).

**Failure modes:** a future scoped `DbContext` injected into a singleton worker would be a captive dependency. When migrating [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17) to EF Core, workers should create scopes per job.

## Model Binding And Validation

**Real code:** Minimal API binds request records such as [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L5-L38) into handlers like [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83). Validation is manual in endpoints: customer name/email [Program.cs](../../../src/Billing.Api/Program.cs#L69-L72), plan prices [Program.cs](../../../src/Billing.Api/Program.cs#L89-L97), coupon discounts [Program.cs](../../../src/Billing.Api/Program.cs#L126-L139), usage quantity [Program.cs](../../../src/Billing.Api/Program.cs#L180-L183).

**Failure modes:** validation is split between endpoint and store. That is okay when endpoint validates input shape and store protects invariants, as with duplicate customer email in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76). It becomes risky when neither boundary owns a rule.

## React Render Model

**Concept:** React renders from state; effects synchronize with external systems.  
**Real code:** `dashboard`, forms, and busy/error state live in [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L47). Initial data fetch is an effect in [App.tsx](../../../src/Billing.Web/src/App.tsx#L53-L55). Derived lookup maps use `useMemo` in [App.tsx](../../../src/Billing.Web/src/App.tsx#L49-L51).

**Failure modes:** `refresh` has no abort handling in [App.tsx](../../../src/Billing.Web/src/App.tsx#L64-L71); stale responses could win if multiple requests overlap. `runAction` serializes user actions with one `busy` flag in [App.tsx](../../../src/Billing.Web/src/App.tsx#L73-L84), which is simple but coarse.

## Data Fetching

**Real code:** The fetch wrapper throws on non-2xx and parses error JSON in [api.ts](../../../src/Billing.Web/src/api.ts#L160-L177). Writes attach idempotency keys in [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158).

**Drill:** Explain why API client errors become UI banner text through [App.tsx](../../../src/Billing.Web/src/App.tsx#L64-L84) and [App.tsx](../../../src/Billing.Web/src/App.tsx#L166-L183).

