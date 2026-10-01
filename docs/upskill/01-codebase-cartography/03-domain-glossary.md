> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/01-codebase-cartography/03-domain-glossary.md.txt).

# Domain Glossary

| Term | Meaning here | Anchor | Transferable judgment |
| --- | --- | --- | --- |
| Customer | Billable account with name/email/currency. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L8) | Customer currency becomes a reporting/migration concern. |
| Plan | Sellable pricing package with cycle, pricing model, base price, included units, unit price. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L10-L20) | Plan edits need versioning in production. |
| Coupon | Discount with percent and/or amount. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L22-L30) | Discount math must be bounded and deterministic. |
| Subscription | Customer-plan relationship and current period. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L52-L60) | Period transitions are consistency boundaries. |
| UsageEvent | Metered usage that may later attach to an invoice. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L62-L69) | Idempotency/deduplication matters for external usage ingestion. |
| InvoiceLine | Priced line item. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L71-L76) | Lines are audit records, not just display rows. |
| Invoice | Bill with lines, totals, period, due date, paid date. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L78-L92) | Totals should be reproducible after plan changes. |
| Payment | Provider attempt/result for an invoice. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L94-L103) | Payment state is eventually consistent. |
| WebhookEvent | Outbound event queued for delivery. | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123) | This is an outbox-shaped model. |
| Money | Minor units plus currency. | [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57) | Avoid floats for financial totals. |
| Idempotency key | Client-provided replay key for unsafe writes. | [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158), [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L57) | Retries must not duplicate money events. |

