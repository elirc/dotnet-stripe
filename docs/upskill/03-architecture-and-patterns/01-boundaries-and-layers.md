# Boundaries And Layers

| Boundary | Owns | Must not own | Evidence | Boundary leak to watch |
| --- | --- | --- | --- | --- |
| React UI | Forms, local state, table rendering | Billing invariants | [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L395) | UI sends numeric strings converted to numbers in [App.tsx](../../../src/Billing.Web/src/App.tsx#L94-L105); server still validates in [Program.cs](../../../src/Billing.Api/Program.cs#L87-L120). |
| API endpoints | HTTP binding, request validation, response status | Long-running business rules | [Program.cs](../../../src/Billing.Api/Program.cs#L67-L250) | Endpoint creates subscription period directly in [Program.cs](../../../src/Billing.Api/Program.cs#L162-L164); consider moving period policy into engine. |
| Domain | Vocabulary and value invariants | HTTP status or UI display | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123), [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57) | `InvoiceResponse` maps domain to public DTO in [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L38-L71), but some raw domain records are returned in dashboard [Program.cs](../../../src/Billing.Api/Program.cs#L53-L62). |
| Store | Process-local data isolation | Business computation | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L264) | Seed data in store [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L323) is fine for learning but not production architecture. |
| Engine | Billing cycle, invoice totals, payment state | HTTP body parsing | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7-L204) | Engine writes multiple records with no transaction in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105). |
| Workers | Deferred side effects | User-facing request validation | [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L36), [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6-L56) | Workers depend on singleton store; EF migration needs scoped units of work. |

## Drill

Pick [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174). Annotate which lines are HTTP concerns, which are billing policy, and which should move if this API grew.

