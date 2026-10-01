> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/06-contribution-practice/01-good-first-tickets.md.txt).

# Good-First Tickets

## Ticket 1: Add Customer Email Format Validation
**Difficulty:** Easy **Time:** 30m **Skills:** validation. **Story:** As an operator, I want invalid emails rejected so customer records are clean. **Read first:** [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83). **Acceptance criteria:** rejects obvious invalid email; existing valid create still works. **Files likely touched:** [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83). **Review questions:** is this too strict for real email?

## Ticket 2: Normalize Coupon Currency Default
**Difficulty:** Easy **Time:** 30m **Read first:** [Program.cs](../../../src/Billing.Api/Program.cs#L124-L151). **Acceptance criteria:** amount-off coupon defaults blank currency to USD like customers in [Program.cs](../../../src/Billing.Api/Program.cs#L292-L293). **Risk:** inconsistent currency semantics.

## Ticket 3: Add Empty Metric Validation
**Difficulty:** Easy **Time:** 30m **Read first:** [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202), [App.tsx](../../../src/Billing.Web/src/App.tsx#L133-L142). **Acceptance criteria:** server either rejects blank metric or documents default. **Review:** endpoint and UI agree.

## Ticket 4: Improve Duplicate Email Error Text
**Difficulty:** Easy **Time:** 20m **Read first:** [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76), [api.ts](../../../src/Billing.Web/src/api.ts#L160-L177). **Acceptance criteria:** user sees actionable error. **Risk:** leaking account existence in real auth context.

## Ticket 5: Add Status Style For Draft
**Difficulty:** Easy **Time:** 20m **Read first:** [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31), [styles.css](../../../src/Billing.Web/src/styles.css#L198-L222). **Acceptance criteria:** `Draft` has explicit badge style.

## Ticket 6: Add Loading State Text To Buttons
**Difficulty:** Easy **Time:** 45m **Read first:** [App.tsx](../../../src/Billing.Web/src/App.tsx#L73-L84), [App.tsx](../../../src/Billing.Web/src/App.tsx#L170-L178). **Acceptance criteria:** busy buttons communicate action without layout shift.

## Ticket 7: Extract `CurrencyInput` UI Helper
**Difficulty:** Easy **Time:** 1h **Read first:** plan form [App.tsx](../../../src/Billing.Web/src/App.tsx#L203-L221). **Acceptance criteria:** no behavior change; less duplication.

## Ticket 8: Add API Health Card
**Difficulty:** Easy **Time:** 1h **Read first:** health endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L39-L39), dashboard load [App.tsx](../../../src/Billing.Web/src/App.tsx#L53-L71). **Acceptance criteria:** UI can show API up/down.

## Ticket 9: Add `CreatedAt` Column To Customers Table
**Difficulty:** Easy **Time:** 45m **Read first:** customer model [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L8), table helpers [App.tsx](../../../src/Billing.Web/src/App.tsx#L390-L395). **Acceptance criteria:** formatted date.

## Ticket 10: Make Webhook Note Less Wide
**Difficulty:** Easy **Time:** 20m **Read first:** webhook table [App.tsx](../../../src/Billing.Web/src/App.tsx#L335-L358), `.truncate` [styles.css](../../../src/Billing.Web/src/styles.css#L230-L235). **Acceptance criteria:** mobile stays readable.

## Ticket 11: Document Idempotency Header In UI Dev Notes
**Difficulty:** Easy **Time:** 30m **Read first:** [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158), [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L57). **Acceptance criteria:** README or docs mention write retry behavior.

## Ticket 12: Add Plan Code Helper Text
**Difficulty:** Easy **Time:** 30m **Read first:** plan create [Program.cs](../../../src/Billing.Api/Program.cs#L87-L120), plan form [App.tsx](../../../src/Billing.Web/src/App.tsx#L203-L221). **Acceptance criteria:** operators know code is lowercased.

## Ticket 13: Add Outstanding Invoice Count To UI
**Difficulty:** Easy **Time:** 45m **Read first:** metrics [Program.cs](../../../src/Billing.Api/Program.cs#L254-L290), metrics UI [App.tsx](../../../src/Billing.Web/src/App.tsx#L185-L191). **Acceptance criteria:** uses existing metric, no API change.

## Ticket 14: Add Friendly Empty Tables
**Difficulty:** Easy **Time:** 1h **Read first:** tables [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358). **Acceptance criteria:** empty arrays render helpful row.

## Ticket 15: Add `Metric` Type Union
**Difficulty:** Easy **Time:** 45m **Read first:** usage event request [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L25-L25), UI sender [App.tsx](../../../src/Billing.Web/src/App.tsx#L133-L142). **Acceptance criteria:** known metric values documented.

## Ticket 16: Make `WebhookWorkerIntervalSeconds` Visible In Docs
**Difficulty:** Easy **Time:** 20m **Read first:** options [BillingOptions.cs](../../../src/Billing.Api/Infrastructure/BillingOptions.cs#L3-L8), appsettings [appsettings.json](../../../src/Billing.Api/appsettings.json#L3-L9). **Acceptance criteria:** config docs explain worker cadence.

## Ticket 17: Add Error Shape Type
**Difficulty:** Easy **Time:** 45m **Read first:** `BadRequest` helper [Program.cs](../../../src/Billing.Api/Program.cs#L292-L296), client parser [api.ts](../../../src/Billing.Web/src/api.ts#L171-L177). **Acceptance criteria:** shared documented shape `{ error: string }`.

## Ticket 18: Add Unit Price Precision Note
**Difficulty:** Easy **Time:** 30m **Read first:** [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L12-L16), architecture note [architecture.md](../../../docs/architecture.md#L18-L20). **Acceptance criteria:** docs explain cent-level pricing.

## Ticket 19: Add Customer Currency Badge
**Difficulty:** Easy **Time:** 45m **Read first:** customer model [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L8), dashboard response [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L11-L22). **Acceptance criteria:** UI displays currency consistently.

## Ticket 20: Add Webhook Attempt Sort Note
**Difficulty:** Easy **Time:** 30m **Read first:** webhook events returned by [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L59-L62), table slice [App.tsx](../../../src/Billing.Web/src/App.tsx#L335-L358). **Acceptance criteria:** learner explains sort order and slice.

