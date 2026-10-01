> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/03-architecture-and-patterns/03-validation-auth-and-permissions.md.txt).

# Validation, Auth, And Permissions

## Validation Layers

Endpoint validation handles request shape: customer fields [Program.cs](../../../src/Billing.Api/Program.cs#L69-L72), plan prices [Program.cs](../../../src/Billing.Api/Program.cs#L89-L97), coupon discount fields [Program.cs](../../../src/Billing.Api/Program.cs#L126-L139), usage quantity [Program.cs](../../../src/Billing.Api/Program.cs#L180-L183). Store validation protects invariants: duplicate customer email [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76), duplicate plan code [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L78-L90), existing customer/plan/coupon for subscription [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L106-L119).

## Auth Boundary

Authentication and authorization are not configured. Evidence: service registration includes options, clock, store, engine, idempotency, verifier, workers, and CORS in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L30), then middleware uses CORS and idempotency only in [Program.cs](../../../src/Billing.Api/Program.cs#L32-L35). Treat every endpoint in [Program.cs](../../../src/Billing.Api/Program.cs#L41-L250) as unauthenticated until proven otherwise.

## Junior Misses Versus Senior Checks

| Area | Junior miss | Senior check | Anchor |
| --- | --- | --- | --- |
| IDOR | “GUIDs are hard to guess.” | Every invoice/customer/subscription ID needs tenant/resource ownership checks. | Routes accept IDs in [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221) and [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L27-L29). |
| Webhook auth | “It has HMAC, so done.” | Verify timestamp, event replay, event ID dedupe, state transitions. | HMAC check in [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L38), apply result in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199). |
| CORS | “CORS is security.” | CORS protects browsers, not API access from scripts/services. | CORS policy in [Program.cs](../../../src/Billing.Api/Program.cs#L21-L30). |
| Validation | “Client form required fields are enough.” | Server validates and domain/store owns invariants. | Client forms in [App.tsx](../../../src/Billing.Web/src/App.tsx#L184-L260), server checks in [Program.cs](../../../src/Billing.Api/Program.cs#L67-L202). |

## Drill

Write an authorization policy plan for `POST /api/invoices/{invoiceId}/pay` using [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221). Define principal, tenant claim, invoice ownership query, denial status, and tests.

