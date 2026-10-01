# Architecture Critique

## Strong Choices

| Choice | Evidence | Why it helps |
| --- | --- | --- |
| Money value object | [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57) | Centralizes currency and rounding rules. |
| Idempotency boundary | [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L88) | Teaches retry-safe writes. |
| Clock abstraction | [BillingClock.cs](../../../src/Billing.Api/Domain/BillingClock.cs#L3-L10), [Program.cs](../../../src/Billing.Api/Program.cs#L14-L14) | Makes billing tests possible. |
| Hosted workers | [Program.cs](../../../src/Billing.Api/Program.cs#L19-L20) | Separates deferred side effects from request path. |
| Typed client | [api.ts](../../../src/Billing.Web/src/api.ts#L3-L143) | Makes UI/API contract visible. |

## Risks And Tradeoffs

| Priority | Risk | Evidence | Impact | Confidence |
| --- | --- | --- | --- | --- |
| 1 | No auth/tenant boundary | [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35) | Any caller can mutate billing data. | High |
| 2 | In-memory persistence | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17) | Process restart loses data and queues. | High |
| 3 | Non-transactional invoice generation | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105) | Partial writes corrupt billing state. | High |
| 4 | Hard-coded USD reporting | [Program.cs](../../../src/Billing.Api/Program.cs#L262-L278) | Incorrect multi-currency metrics. | High |
| 5 | Simulated webhook delivery | [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39) | No real timeout/retry/signature behavior. | High |
| 6 | No tests/CI found | Project files only in [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19) | Refactors are manual-risk. | Medium |

## If I Owned This For 3 Months

1. Add auth and tenant/resource authorization around all endpoints in [Program.cs](../../../src/Billing.Api/Program.cs#L41-L250). Test IDOR on invoice pay [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221). Rollback: feature flag auth in dev only at first.
2. Introduce database-backed store behind current [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L264) API. Migration: shadow-write in memory and database during learning. Test: invoice generation transaction around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105).
3. Add durable outbox for [WebhookEvent](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123) and replace simulated delivery in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39).
4. Add test projects for `Money` [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57), `BillingEngine` [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204), idempotency [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L122), and React actions [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L154).
5. Add observability: structured logs around endpoint failures [Program.cs](../../../src/Billing.Api/Program.cs#L79-L81), worker drops [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L27-L31), and webhook retries [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L55).

## Confirmed Versus Hypothesis

Confirmed: no product auth wiring in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35), in-memory persistence in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17), API uses string enum JSON in [Program.cs](../../../src/Billing.Api/Program.cs#L10-L13). Hypothesis: no tests/CI based on file inventory; verify again before making a repository-wide claim.

