> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/06-contribution-practice/04-refactor-and-design-katas.md.txt).

# Refactor And Design Katas

1. **Boundary leak:** raw domain records are returned in dashboard [Program.cs](../../../src/Billing.Api/Program.cs#L53-L62). Design DTOs without breaking UI types in [api.ts](../../../src/Billing.Web/src/api.ts#L16-L143).
2. **Outbox/eventing:** turn [WebhookEvent](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123) plus [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L220-L264) into durable outbox design.
3. **Split a large module:** [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L395) is one file. Propose component boundaries that preserve data ownership.
4. **Improve type safety:** replace string metric in [UsageEvent](../../../src/Billing.Api/Domain/BillingModels.cs#L62-L69) and [CreateUsageEventRequest](../../../src/Billing.Api/Contracts/Requests.cs#L25-L25) with a safer model.
5. **Design a migration:** persist `Money` from [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57) as columns. Include constraints.
6. **Reduce future N+1:** if EF replaces [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62), design dashboard projections for [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63).
7. **Write an RFC:** move subscription period policy from [Program.cs](../../../src/Billing.Api/Program.cs#L162-L164) into [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7-L204).
8. **Review flawed PR:** a PR removes locks from [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L264). Write blocking findings.
9. **Improve idempotency:** add user/tenant scope to [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L40-L45) after auth exists.
10. **State machine:** formalize invoice transitions around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L199).

