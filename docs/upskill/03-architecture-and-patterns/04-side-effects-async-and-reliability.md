# Side Effects, Async, And Reliability

## Current Side Effects

| Side effect | Trigger | Current implementation | Risk |
| --- | --- | --- | --- |
| Invoice-created webhook | Invoice generation | Enqueued in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L100-L113). | Placeholder destination, no real delivery. |
| Payment job | Pay invoice | Enqueued in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L152-L153). | In-memory queue lost on crash. |
| Payment result | Worker/provider webhook | Applied in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199). | Late/duplicate events not deduped by provider event ID. |
| Webhook delivery | Hosted worker | Simulated success in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39). | No HTTP timeout/retry policy/signature. |

## Reliability Concepts

**Idempotency:** unsafe API methods can replay a successful response through [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L88). The browser always sends a key on writes in [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158).

**Retries:** outbound webhook failure path backs off with exponential delay in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L55), but the success path is simulated and does not exercise HTTP failures.

**Outbox:** `WebhookEvent` stores payload, status, attempt, timestamps, and error in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123). The store queues event IDs in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L220-L264).

**Timeouts:** no `IHttpClientFactory` or HTTP sender exists; this is a design gap anchored to simulated delivery in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39).

## Drill

For invoice generation [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105), mark which writes must be atomic and which side effects can be eventually delivered.

