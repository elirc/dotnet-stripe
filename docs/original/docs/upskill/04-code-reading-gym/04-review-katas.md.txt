# Review Katas

## Kata 1: Add Customer Phone
**Author intent:** Add phone to customers. **Fake diff summary:** adds `Phone` to `Customer` and form. **Resembles:** [Customer](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L8), [create customer](../../../src/Billing.Api/Program.cs#L67-L83), [form](../../../src/Billing.Web/src/App.tsx#L184-L193). **Expected findings:** Important: API contract versioning, validation, display, tests. **Good review comment:** > Where is phone validated server-side, and should existing seeded customers in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L302-L306) get a default?

## Kata 2: Allow Negative Coupons
**Resembles:** coupon validation [Program.cs](../../../src/Billing.Api/Program.cs#L124-L151), discount math [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L31-L48). **Expected findings:** Blocking: negative amount can increase invoice totals or break invariants. **Good review comment:** > This violates the discount invariant currently guarded by [Program.cs](../../../src/Billing.Api/Program.cs#L136-L139). What product case needs this?

## Kata 3: Remove Idempotency Header
**Resembles:** client writes [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158), middleware [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L88). **Expected findings:** Blocking: retry safety regression. **Good review comment:** > Billing writes are not safe to duplicate; please preserve idempotency or explain replacement semantics.

## Kata 4: Real Webhook HTTP Sender
**Resembles:** simulated success [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39). **Expected findings:** Blocking: needs timeout, status handling, retry, logging, signing. **Good review comment:** > How will we observe failed delivery and avoid infinite fast retries? Current failure state is in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L55).

## Kata 5: Move Billing Period Logic To UI
**Resembles:** period calculation [Program.cs](../../../src/Billing.Api/Program.cs#L162-L164). **Expected findings:** Blocking: client cannot own billing policy. **Good review comment:** > Billing period rules should stay server-side because clients are untrusted and multiple clients may exist.

## Kata 6: Add EF DbContext Into Singleton Store
**Resembles:** singleton store registration [Program.cs](../../../src/Billing.Api/Program.cs#L15-L16), store class [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17). **Expected findings:** Blocking: captive dependency/lifetime bug. **Good review comment:** > A scoped DbContext must not be captured by this singleton; create a scope per operation or change the lifetime design.

## Kata 7: Cache Dashboard Forever
**Resembles:** dashboard endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63), React refresh [App.tsx](../../../src/Billing.Web/src/App.tsx#L64-L71). **Expected findings:** Important: invalidation after writes. **Good review comment:** > What invalidates cache after [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L154) mutations?

## Kata 8: Add New Invoice Status
**Resembles:** enum [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31), TS union [api.ts](../../../src/Billing.Web/src/api.ts#L5-L5), badge CSS [styles.css](../../../src/Billing.Web/src/styles.css#L198-L222). **Expected findings:** Important: cross-layer contract update. **Good review comment:** > Please update TypeScript status types and visual treatment, and add a test showing the new status round-trips.

## Kata 9: Charge Payment Synchronously
**Resembles:** accepted response [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221), worker [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L35). **Expected findings:** Important: latency and retry semantics. **Good review comment:** > What happens if provider latency spikes? Current design separates queueing from completion.

## Kata 10: Remove Store Lock
**Resembles:** lock gate [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17) and operations [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L264). **Expected findings:** Blocking: thread safety regression. **Good review comment:** > Hosted workers and API requests can access the store concurrently; please prove thread safety before removing the lock.

