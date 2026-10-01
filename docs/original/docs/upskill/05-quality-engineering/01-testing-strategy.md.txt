# Testing Strategy

## Layers

| Layer | What belongs | Repo anchor | What not to test |
| --- | --- | --- | --- |
| Unit | Pure money/coupon math | [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57), [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L31-L48) | ASP.NET wiring. |
| Service | Invoice generation/payment transitions | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204) | Browser layout. |
| Middleware | Idempotency replay/conflict | [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L88) | Billing totals. |
| Integration | Minimal API endpoints | [Program.cs](../../../src/Billing.Api/Program.cs#L39-L250) | CSS details. |
| UI component | Form actions, status tables | [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L154), [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358) | Backend invariants. |
| E2E | dashboard load, run cycle, pay invoice | [api.ts](../../../src/Billing.Web/src/api.ts#L145-L184), [Program.cs](../../../src/Billing.Api/Program.cs#L41-L221) | Exhaustive edge cases. |

## Test Data

Control time through `IBillingClock` [BillingClock.cs](../../../src/Billing.Api/Domain/BillingClock.cs#L3-L10), not `DateTimeOffset.UtcNow`. Replace seeded data [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L323) with explicit builders in tests.

## Flake Prevention

Avoid waiting for real `PeriodicTimer` intervals in [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L24). Test worker processing through small extracted methods or fake clock/job store. Do not assert exact generated GUIDs except seeded IDs in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L268-L306).

