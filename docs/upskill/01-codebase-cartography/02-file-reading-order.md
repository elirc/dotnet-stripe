> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/01-codebase-cartography/02-file-reading-order.md.txt).

# File Reading Order

## Junior Path

| # | File | Why | Look for |
| --- | --- | --- | --- |
| 1 | [README.md](../../../README.md#L1-L54) | Product intent and commands. | Which commands are verified vs not. |
| 2 | [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L1-L7) | Target framework and language defaults. | `net8.0`, nullable refs. |
| 3 | [Program.cs](../../../src/Billing.Api/Program.cs#L7-L37) | Composition root. | DI lifetimes and middleware order. |
| 4 | [Program.cs](../../../src/Billing.Api/Program.cs#L41-L250) | Endpoint list. | Inputs, validation, errors. |
| 5 | [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L5-L38) | API input contracts. | What the client can send. |
| 6 | [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L5-L71) | API output contracts. | DTO/domain coupling. |
| 7 | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123) | Domain nouns. | Records and statuses. |
| 8 | [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57) | Money handling. | Currency invariant and rounding. |
| 9 | [api.ts](../../../src/Billing.Web/src/api.ts#L145-L184) | Fetch wrapper. | Error handling and idempotency. |
| 10 | [App.tsx](../../../src/Billing.Web/src/App.tsx#L64-L154) | Client actions. | How forms call API. |

## Mid-Level Path

Read [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L264) for data ownership, [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204) for billing invariants, [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L122) for HTTP safety, [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L7-L38) for signed input, and [styles.css](../../../src/Billing.Web/src/styles.css#L1-L265) for responsive UI constraints.

## Senior Path

Start with missing boundaries: in-memory seed data [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L323), no auth registration in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35), simulated delivery in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39), and MRR hard-coded to USD in [Program.cs](../../../src/Billing.Api/Program.cs#L262-L278). Convert each into a risk, migration, and test plan.

