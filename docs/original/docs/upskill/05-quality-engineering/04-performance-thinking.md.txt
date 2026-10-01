# Performance Thinking

Measure first. This repo is small, so most items are “possible risk when scaled,” not current bugs.

| Domain | Likely hotspot | Evidence | What to measure |
| --- | --- | --- | --- |
| Render | Large tables render all rows | Invoice/subscription/plan/webhook tables in [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358) | React render time, row count. |
| Network | Dashboard aggregate returns everything | [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63) | Payload size, latency. |
| Server | Store read methods copy arrays | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62) | allocations/request. |
| DB future | Unbilled usage query | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L178-L189) | index use, N+1, scan count after EF migration. |
| Worker | tight dequeue loops | [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L19-L23), [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L19-L23) | jobs/tick, CPU. |
| Bundle | No route splitting | entry renders one app in [main.tsx](../../../src/Billing.Web/src/main.tsx#L1-L10) | bundle size after `npm run build`. |
| Startup | Seed data generated in constructor | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L19-L22), [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L323) | startup time if seed grows. |

## Drill

Set a budget: dashboard payload under 250 KB and first render under 1 second. Identify which lines would need pagination: [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63), [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62), [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358).

