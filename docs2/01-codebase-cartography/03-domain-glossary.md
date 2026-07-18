# 01.03 — Domain Glossary

The vocabulary of this system, with the exact place each term lives in code. When a term has a
precise meaning *in this repo* that differs from the industry default, that's flagged.

## Core entities

| Term | What it means here | Defined at | Notes |
| --- | --- | --- | --- |
| **Customer** | A billable account (a company). Has a name, email, currency, created date. | [Customer](../../src/Billing.Api/Domain/BillingModels.cs#L3-L8) | Email is the uniqueness key ([store L68](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68)). No "tenant" concept above it. |
| **Plan** | A priced product: cycle (monthly/annual), pricing model, base price, included units, unit price. | [Plan](../../src/Billing.Api/Domain/BillingModels.cs#L10-L20) | `Code` is the unique key, lowercased ([Program L104](../../src/Billing.Api/Program.cs#L104)). |
| **PricingModel** | `Flat` (base only), `Metered` (usage only), `Hybrid` (base + usage). | [enum](../../src/Billing.Api/Domain/BillingEnums.cs#L9-L14) | Drives which invoice lines get built ([engine L36-L60](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L36-L60)). |
| **Coupon** | A discount: either `PercentOff` or `AmountOff`, optional expiry. | [Coupon](../../src/Billing.Api/Domain/BillingModels.cs#L22-L50) | Knows how to compute its own discount ([CalculateDiscount](../../src/Billing.Api/Domain/BillingModels.cs#L31-L49)). Code uppercased. |
| **Subscription** | A customer's active enrolment in a plan, with a current billing period and optional coupon. | [Subscription](../../src/Billing.Api/Domain/BillingModels.cs#L52-L60) | Carries `CurrentPeriodStart/End`; advanced by the engine after invoicing. |
| **UsageEvent** | A metered usage record (quantity of a metric at a time) for a subscription. | [UsageEvent](../../src/Billing.Api/Domain/BillingModels.cs#L62-L69) | `InvoiceId == null` means *unbilled* — the engine's selection key ([store L184](../../src/Billing.Api/Infrastructure/BillingStore.cs#L184)). |
| **Invoice** | A generated bill: line items, subtotal, discount, total, period, due date, status. | [Invoice](../../src/Billing.Api/Domain/BillingModels.cs#L78-L92) | Immutable record; "updating" = `with { … }` + re-save. |
| **InvoiceLine** | One row on an invoice: description, qty, unit amount, amount, `Kind` (`recurring`/`usage`). | [InvoiceLine](../../src/Billing.Api/Domain/BillingModels.cs#L71-L76) | `Kind` is a bare string, not an enum. |
| **Payment** | A record of a payment attempt on an invoice, with provider + status. | [Payment](../../src/Billing.Api/Domain/BillingModels.cs#L94-L103) | A *new* Payment row is written per attempt — there can be several per invoice. |
| **PaymentJob** | A queued instruction to process payment for an invoice. | [PaymentJob](../../src/Billing.Api/Domain/BillingModels.cs#L105-L110) | Lives only in the `_paymentJobs` queue; has `AvailableAt` for delay. |
| **WebhookEvent** | An *outbound* webhook to be delivered, with status/attempt/backoff. | [WebhookEvent](../../src/Billing.Api/Domain/BillingModels.cs#L112-L122) | "Outbox row". Distinct from the *inbound* provider webhook (an HTTP request, not an entity). |

## Money & metrics

| Term | Meaning here | Anchor |
| --- | --- | --- |
| **Money** | A value type: `long MinorUnits` + ISO `Currency`. `$12.34` → `1234` minor units, `"USD"`. | [Money](../../src/Billing.Api/Domain/Money.cs#L5-L68) |
| **Minor units** | The smallest currency unit (cents for USD). All money math is integer math on these. | [Money L7, L12-L16](../../src/Billing.Api/Domain/Money.cs#L7-L16) |
| **MRR** | Monthly Recurring Revenue: sum of active subscriptions' monthly-normalized base price. | [CalculateMetrics L262-L274](../../src/Billing.Api/Program.cs#L262-L274) |
| **Outstanding** | Sum of `Open` invoice totals. | [L276-L278](../../src/Billing.Api/Program.cs#L276-L278) |
| **Paid invoice rate** | `Paid / total` invoices, as a rounded %. | [L280-L281](../../src/Billing.Api/Program.cs#L280-L281) |
| **MajorUnits** | The human-facing decimal (`MinorUnits / 100`). | [Money L29](../../src/Billing.Api/Domain/Money.cs#L29) — **note the hardcoded `/ 100`: assumes 2-decimal currencies.** |

## Process / reliability vocabulary

| Term | Meaning here | Anchor |
| --- | --- | --- |
| **Idempotency key** | A client-supplied `Idempotency-Key` header that makes a repeated POST replay the first response instead of re-executing. | [middleware](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L68) |
| **Replay** | Returning the cached first response for a matching key+body (adds `Idempotency-Replayed: true`). | [L44-L47](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L44-L47) |
| **Outbox** | The pattern of writing "to-send" messages to storage, then a worker delivers them. Here: `WebhookEvent` rows drained by the worker. *(Simulated — no real HTTP.)* | [worker](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L26-L54) |
| **Billing cycle / run-cycle** | A sweep that invoices every active subscription whose period has ended. | [RunBillingCycle](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127) |
| **Signing secret** | The shared HMAC key for verifying inbound provider webhooks. | [BillingOptions L5](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L5) |
| **The clock** | `IBillingClock` — time is injected, not read from `DateTime.UtcNow` directly (mostly). | [IBillingClock](../../src/Billing.Api/Domain/BillingClock.cs#L3-L11) — but the verifier uses `DateTimeOffset.UtcNow` directly ([L25](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L25)). |

## Status enums (the state machines)

| Enum | Values | Which are actually used? |
| --- | --- | --- |
| [InvoiceStatus](../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31) | Draft, Open, Paid, Void, Uncollectible | Only **Open** and **Paid** are ever set. Draft/Void/Uncollectible are dead. |
| [SubscriptionStatus](../../src/Billing.Api/Domain/BillingEnums.cs#L16-L22) | Trialing, Active, PastDue, Canceled | Only **Active** is ever set (seed + create). **PastDue/Canceled/Trialing never assigned at runtime.** |
| [PaymentStatus](../../src/Billing.Api/Domain/BillingEnums.cs#L33-L38) | Pending, Succeeded, Failed | All three reachable (Pending on queue, then Succeeded/Failed). |
| [WebhookDeliveryStatus](../../src/Billing.Api/Domain/BillingEnums.cs#L40-L45) | Pending, Succeeded, Failed | Pending→Succeeded always; **Failed only on the dead retry path**. |

> **Glossary-level senior observation:** the gap between *defined* states and *reached* states is a
> map of unfinished features. `PastDue`, `Void`, `Uncollectible`, and the dunning logic that would
> set them, do not exist yet. That's not a bug — it's scope. Knowing the difference is the skill.

## Verification Notes

All anchors read-verified on 2026-06-09. "Which states are used" was determined by reading every
assignment site of each enum across the codebase (grep-equivalent reading of `Program.cs`,
`BillingEngine.cs`, `BillingStore.cs`, workers). Not executed.
