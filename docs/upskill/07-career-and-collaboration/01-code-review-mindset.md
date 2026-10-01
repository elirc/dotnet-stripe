> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/07-career-and-collaboration/01-code-review-mindset.md.txt).

# Code Review Mindset

Review in this order:

1. **Does it work?** Trace from UI [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L154) to API [Program.cs](../../../src/Billing.Api/Program.cs#L67-L250).
2. **Is it correct?** Check invariants in [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57), [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L31-L48), and [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L129).
3. **Will it stay correct?** Look for missing tests around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204) and idempotency [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L122).
4. **Does it fit?** Respect boundaries in [Program.cs](../../../src/Billing.Api/Program.cs#L7-L37), [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L264), and [api.ts](../../../src/Billing.Web/src/api.ts#L145-L184).
5. **Is it kind to maintainers?** Small diff, clear naming, docs, tests, no drive-by refactors.

Good comment examples:

> Blocking: this changes invoice payment state without considering the existing guard in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L141). Please add a state-transition test.

> Important: this adds a new enum value in [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31), but the TypeScript union in [api.ts](../../../src/Billing.Web/src/api.ts#L5-L5) and status CSS in [styles.css](../../../src/Billing.Web/src/styles.css#L198-L222) were not updated.

> Optional: the endpoint is getting long near [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174). A follow-up could move period policy into the engine.

