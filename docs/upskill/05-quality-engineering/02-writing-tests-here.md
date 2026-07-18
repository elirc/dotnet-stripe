# Writing Tests Here

These commands are inferred because no test project exists in [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19).

## Recipes

1. **Money happy path:** test `Money.FromMajor`, `Add`, `Subtract`, and currency mismatch in [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L12-L57). Command after adding tests: `dotnet test --filter Money`.
2. **Coupon cap:** test discount never exceeds subtotal in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L31-L48). Command: `dotnet test --filter Coupon`.
3. **Validation failure:** POST invalid usage quantity to [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202). Command: `dotnet test --filter Usage`.
4. **Idempotency replay:** same key/body should replay from [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L40-L57). Command: `dotnet test --filter Idempotency`.
5. **Idempotency conflict:** same key/different body should return conflict from [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L39). Command: `dotnet test --filter IdempotencyConflict`.
6. **Billing cycle:** seed one hybrid subscription and usage, then assert invoice lines from [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L36-L64). Command: `dotnet test --filter BillingCycle`.
7. **Async side effect:** queue payment through [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L154), then process job behavior equivalent to [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L20-L35). Command: `dotnet test --filter PaymentWorker`.
8. **UI state:** mock [api.ts](../../../src/Billing.Web/src/api.ts#L145-L184), click run cycle in [App.tsx](../../../src/Billing.Web/src/App.tsx#L145-L149), assert refresh/error behavior from [App.tsx](../../../src/Billing.Web/src/App.tsx#L73-L84). Command after adding tooling: `npm test -- App`.

## Permission Tests To Add After Auth

Cross-tenant rejection belongs around invoice pay [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221), subscription creation [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174), and customer reads [Program.cs](../../../src/Billing.Api/Program.cs#L65-L65).

