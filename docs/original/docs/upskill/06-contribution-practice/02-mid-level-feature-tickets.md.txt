# Mid-Level Feature Tickets

## Ticket 1: Add Customer Detail View
**Difficulty:** Medium **Time:** 1-2d **Story:** As an operator, I want one customer page with subscriptions and invoices. **Read first:** dashboard endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63), models [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L92), tables [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358). **Acceptance:** API endpoint, typed client method, UI route/state, tests. **Risk:** unbounded data; add projection.

## Ticket 2: Add Plan Archive
**Difficulty:** Medium **Time:** 1d **Read first:** `IsActive` [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L10-L20), plan create [Program.cs](../../../src/Billing.Api/Program.cs#L87-L120). **Acceptance:** archived plans cannot be newly subscribed; existing subscriptions remain. **Rollback:** re-enable plan.

## Ticket 3: Add Invoice Void
**Difficulty:** Medium **Time:** 1d **Read first:** invoice status enum [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31), pay guard [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L141). **Acceptance:** open invoice can void; paid cannot. **Tests:** state transitions.

## Ticket 4: Add Usage Event Idempotency
**Difficulty:** Medium **Time:** 1-2d **Read first:** usage endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202), store add [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L122-L129), idempotency middleware [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L88). **Acceptance:** provider event ID dedupes usage.

## Ticket 5: Add Webhook Event ID Dedupe
**Difficulty:** Medium **Time:** 1d **Read first:** webhook request [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L33-L38), endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L223-L250). **Acceptance:** duplicate provider event does not create duplicate payment rows.

## Ticket 6: Add Auth Skeleton
**Difficulty:** Medium **Time:** 2d **Read first:** service registration [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35), endpoints [Program.cs](../../../src/Billing.Api/Program.cs#L41-L250). **Acceptance:** dev API key or JWT policy protects writes. **Risk:** CORS is not auth.

## Ticket 7: Add Store Interface
**Difficulty:** Medium **Time:** 1-2d **Read first:** [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L264), consumers [Program.cs](../../../src/Billing.Api/Program.cs#L41-L250), [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7-L204). **Acceptance:** interface enables fake store tests.

## Ticket 8: Add Invoice Number Sequence
**Difficulty:** Medium **Time:** 1d **Read first:** invoice number generation [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L205-L206). **Acceptance:** deterministic monotonic numbers per month. **Risk:** concurrency.

## Ticket 9: Add Dashboard Pagination
**Difficulty:** Medium **Time:** 2d **Read first:** dashboard endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63), store read methods [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62), tables [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358). **Acceptance:** invoices/webhooks can page.

## Ticket 10: Add Real Webhook Sender
**Difficulty:** Medium **Time:** 2-3d **Read first:** simulated worker [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L55), event model [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123). **Acceptance:** timeout, retry, non-2xx handling, tests.

## Ticket 11: Add React Form Validation State
**Difficulty:** Medium **Time:** 1d **Read first:** form handlers [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L142), API validation [Program.cs](../../../src/Billing.Api/Program.cs#L67-L202). **Acceptance:** client catches basic errors without replacing server validation.

## Ticket 12: Add Test Project
**Difficulty:** Medium **Time:** 1-2d **Read first:** solution [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19), engine [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204), money [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57). **Acceptance:** first passing unit tests and documented command.

