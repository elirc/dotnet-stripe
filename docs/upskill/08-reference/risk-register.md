# Risk Register

| Risk | Evidence | Impact | Likelihood | Suggested test | Suggested fix | Confidence |
| --- | --- | --- | --- | --- | --- | --- |
| No auth/tenant checks | [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35) | Data exposure/mutation | High if deployed | Unauthorized write rejected | Add authn/authz policies | High |
| In-memory persistence | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17) | Data loss on restart | Certain by design | restart loses seed mutations | DB-backed store | High |
| Non-transactional invoice generation | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105) | Partial billing state | Medium | force failure between writes | transaction/outbox | High |
| Process-local idempotency | [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L90-L120) | duplicate writes across instances | High in multi-instance | replay after restart | persistent idempotency table | High |
| Hard-coded USD metrics | [Program.cs](../../../src/Billing.Api/Program.cs#L262-L278) | wrong reporting | Medium | mixed-currency dashboard | per-currency or FX reporting | High |
| Webhook replay gap | [Program.cs](../../../src/Billing.Api/Program.cs#L223-L250) | duplicate payment effects | Medium | duplicate event ID | persist provider event IDs | Medium |
| Direct system clock in verifier | [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L23-L26) | hard tests, clock skew | Low | fake clock verifier test | inject `IBillingClock` | High |
| Always-success payment worker | [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L32-L35) | unrealistic payment behavior | High for learning | provider failure scenario | provider abstraction | High |
| Unbounded dashboard | [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63) | latency/memory | Medium as data grows | large data benchmark | paging/projections | Medium |
| No tests/CI found | [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19), [package.json](../../../src/Billing.Web/package.json#L6-L10) | regressions | High | CI fails on seeded test | add test projects/workflow | Medium |
| Dev secret in config | [appsettings.json](../../../src/Billing.Api/appsettings.json#L3-L9) | secret leakage if reused | Medium | scan for non-dev secret | environment secret | High |
| Raw domain in dashboard | [Program.cs](../../../src/Billing.Api/Program.cs#L53-L62) | contract coupling | Medium | change domain field breaks UI | DTOs for all resources | High |

