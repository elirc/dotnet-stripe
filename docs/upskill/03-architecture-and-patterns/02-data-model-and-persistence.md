# Data Model And Persistence

## Current Data Model

The domain model is normalized conceptually: customers [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L8), plans [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L10-L20), coupons [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L22-L50), subscriptions [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L52-L60), usage [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L62-L69), invoices/lines [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L71-L92), payments [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L94-L103), and webhook events [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123).

## Current Persistence

Persistence is in-memory dictionaries and queues in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17). Methods lock on one gate for isolation in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L264). Seed data lives in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L323).

## EF Core Migration Exercise

EF Core is not present: the API project has no package references in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L1-L7), and the store is custom in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17). A safe migration plan:

1. Add repository/store interface matching current store operations around [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L264).
2. Add tables for records in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123).
3. Preserve `Money` as `MinorUnits` plus `Currency`, anchored to [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57).
4. Make invoice generation transactional around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105).
5. Move queues to durable outbox rows matching [WebhookEvent](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123) and [PaymentJob](../../../src/Billing.Api/Domain/BillingModels.cs#L105-L110).

## Transaction Boundaries

The riskiest transaction is invoice generation: saving invoice, marking usage, rolling subscription, and enqueueing webhook all happen in sequence in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105). In production, these need one database transaction plus an outbox.

## Drill

Design indexes for `UsageEvent`: subscription ID and invoice ID are used by [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L178-L189), so propose indexes before writing EF code.

