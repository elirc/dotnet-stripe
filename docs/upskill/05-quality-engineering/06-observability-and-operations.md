# Observability And Operations

## Current Observability

Workers inject loggers in [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L11) and [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6-L10). Payment worker logs missing invoices in [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L27-L31). Webhook worker logs delivery exceptions in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L43). Health endpoint exists in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L39).

## Missing Operations Surface

No global exception middleware is visible near [Program.cs](../../../src/Billing.Api/Program.cs#L32-L37). No metrics/tracing setup is registered in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L20). No Docker or CI files were found in inventory; see [../08-reference/verification-log.md](../08-reference/verification-log.md).

## “How Would I Know This Broke?”

| Flow | Signal to add | Anchor |
| --- | --- | --- |
| Dashboard load | request count, latency, response size | [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63) |
| Customer creation | validation failure count, duplicate email count | [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83), [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76) |
| Billing cycle | invoices generated, cycle duration, failed cycles | [Program.cs](../../../src/Billing.Api/Program.cs#L204-L209), [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124) |
| Payment | pending job age, paid/failed count | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L199), [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L35) |
| Webhook | pending age, attempts, failure reason | [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L55) |

## Drill

Design three structured log messages for invoice generation around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105): start, success, failure. Include invoice ID, subscription ID, customer ID, period, and total.

