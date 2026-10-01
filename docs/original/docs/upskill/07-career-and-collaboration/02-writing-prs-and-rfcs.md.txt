# Writing PRs And RFCs

## PR Template

```md
## What
[Concrete change]

## Why
[User/operator/developer value]

## How
[Key files, e.g. Program.cs, BillingEngine.cs, App.tsx]

## Tests
[Commands run and results]

## Risks
[Migration/rollback/security/performance]

## Follow-ups
[Explicitly out of scope]
```

Use anchors in descriptions: endpoint changes in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L250), billing logic in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204), UI changes in [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L395).

## Commit Messages

Good: `Add idempotency conflict test for customer creation` because it points to behavior in [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L57). Weak: `fix stuff`.

## When To RFC

Write an RFC for auth around [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35), database migration from [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17), or webhook delivery replacement for [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39). These change architecture commitments and rollback plans.

## RFC Skeleton

Problem, goals, non-goals, current evidence, proposed design, alternatives, migration plan, tests, security/performance impact, rollout/rollback, open questions.

