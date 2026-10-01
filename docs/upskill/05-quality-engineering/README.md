> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/05-quality-engineering/README.md.txt).

# Quality Engineering

This repo currently has no test project in the solution [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19). That makes it useful for learning how to design tests from seams: domain value objects [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57), engine behavior [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204), middleware [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L122), and UI actions [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L154).

Read testing first, then debugging, performance, security, and operations.

