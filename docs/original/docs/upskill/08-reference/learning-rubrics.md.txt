# Learning Rubrics

## Junior

Observable behaviors:

- Can run or explain inferred run commands from [README.md](../../../README.md#L22-L37).
- Can trace one endpoint from [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L154) to [Program.cs](../../../src/Billing.Api/Program.cs#L67-L250).
- Can explain why `Money` exists using [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57).
- Makes small changes without crossing unrelated files.

Self-check: “Can I name inputs, outputs, and validation?”

## Mid-Level

Observable behaviors:

- Designs tests around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204) and [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L122).
- Recognizes boundary splits between [Program.cs](../../../src/Billing.Api/Program.cs#L67-L250), [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L264), and [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L395).
- Updates API and TypeScript contracts together using [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L5-L71) and [api.ts](../../../src/Billing.Web/src/api.ts#L3-L143).
- Reviews for failure modes, not just style.

Self-check: “Can I explain the invariant and the test that protects it?”

## Senior

Observable behaviors:

- Identifies architectural commitments: in-memory state [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17), no auth [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35), non-transactional invoice writes [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105).
- Writes migration, rollout, rollback, observability, and security plans.
- Distinguishes confirmed facts from hypotheses.
- Keeps refactors scoped and teaches others through review comments.

Self-check: “Who owns this risk, what would fail first, and how would we know?”

