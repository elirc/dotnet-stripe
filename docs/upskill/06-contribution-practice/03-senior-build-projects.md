> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/06-contribution-practice/03-senior-build-projects.md.txt).

# Senior Build Projects

## Project 1: Database-Backed Billing Store
**Problem:** current in-memory store [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17) loses data. **Value:** durable billing state. **Design checklist:** schema for [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123), transactions for [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105), indexes for [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L178-L189). **Rollout:** dual-write or one-time migration from seed only. **Rollback:** switch DI back to in-memory store.

## Project 2: Tenant Authorization
**Problem:** endpoints are unauthenticated [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35). **Value:** prevents IDOR and billing data exposure. **Design:** authn scheme, tenant claim, resource ownership checks for IDs in [Program.cs](../../../src/Billing.Api/Program.cs#L155-L221). **Tests:** forbidden cross-tenant pay. **Open question:** single tenant demo mode?

## Project 3: Durable Outbox And Webhook Delivery
**Problem:** webhook queue is in-memory [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L220-L264) and worker simulates success [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39). **Value:** reliable integrations. **Design:** outbox table, leased dequeue, HMAC signing, retry policy, dead-letter state. **Rollback:** keep simulated sender behind feature flag.

## Project 4: Payment Provider Abstraction
**Problem:** payment worker always succeeds [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L32-L35). **Value:** realistic provider integration. **Design:** provider interface, idempotent charge key, timeout, webhook reconciliation, failure states in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199). **Security:** never trust client-provided amount.

## Project 5: Multi-Currency Reporting
**Problem:** metrics aggregate USD only [Program.cs](../../../src/Billing.Api/Program.cs#L262-L278), while customers carry currency [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L8). **Value:** correct finance reporting. **Design:** reporting currency, FX rates, snapshot totals, display currency. **Rollback:** show per-currency metrics only.

## Project 6: Contract-Tested API And UI
**Problem:** TS types mirror API manually [api.ts](../../../src/Billing.Web/src/api.ts#L3-L143), and JSON is trusted [api.ts](../../../src/Billing.Web/src/api.ts#L160-L168). **Value:** safer refactors. **Design:** OpenAPI generation or contract tests, typed client, runtime validation. **Rollout:** start with invoice/dashboard contracts.

## Project 7: Production Observability
**Problem:** only minimal worker logs and health endpoint exist [Program.cs](../../../src/Billing.Api/Program.cs#L39-L39), [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L43). **Value:** faster incident response. **Design:** structured logs, metrics, traces, dashboards, alerts for pending webhook age and payment failures.

