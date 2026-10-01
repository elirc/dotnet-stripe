> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/03-architecture-and-patterns/05-pattern-catalog.md.txt).

# Pattern Catalog

## Pattern: Composition Root
**Problem:** Services and middleware need one discoverable wiring point. **Real example:** [Program.cs](../../../src/Billing.Api/Program.cs#L7-L37). **Why it works:** new readers can see lifetimes and pipeline order. **Failure modes:** composition grows into logic. **Use when:** app startup. **Avoid when:** hiding feature behavior. **Drill:** identify every singleton and why it is safe or risky.

## Pattern: Minimal API Endpoint
**Problem:** Small APIs need low ceremony. **Real example:** customer endpoints [Program.cs](../../../src/Billing.Api/Program.cs#L65-L83). **Second:** invoice pay endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221). **Failure modes:** handlers become too large. **Drill:** split HTTP validation from billing policy on paper.

## Pattern: Immutable Domain Record
**Problem:** Billing state transitions should be explicit. **Real example:** records in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123). **Second:** `with` updates in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L88). **Failure modes:** copying without transaction semantics. **Drill:** annotate one transition.

## Pattern: Value Object For Money
**Problem:** Decimal/floating mistakes corrupt invoices. **Real example:** [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57). **Second:** invoice totals [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L64). **Failure modes:** sub-cent pricing unsupported. **Drill:** design `UnitPriceMicros`.

## Pattern: Manual Validation At Boundary
**Problem:** Bad input should fail before domain mutation. **Real example:** plan validation [Program.cs](../../../src/Billing.Api/Program.cs#L87-L120). **Second:** usage validation [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202). **Failure modes:** inconsistent error shapes. **Drill:** propose a validation helper.

## Pattern: Store-Owned Invariant
**Problem:** Multiple callers need the same uniqueness/existence rule. **Real example:** duplicate email check [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76). **Second:** subscription foreign-key checks [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L106-L119). **Failure modes:** in-memory invariant disappears in DB migration unless backed by constraints. **Drill:** list database constraints.

## Pattern: Idempotency Middleware
**Problem:** Retries must not duplicate writes. **Real example:** [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L88). **Second:** client key generation [api.ts](../../../src/Billing.Web/src/api.ts#L149-L184). **Failure modes:** process-local cache, no user/tenant scope. **Drill:** design table columns.

## Pattern: Clock Abstraction
**Problem:** Time-dependent code needs testability. **Real example:** [BillingClock.cs](../../../src/Billing.Api/Domain/BillingClock.cs#L3-L10). **Second:** engine injection [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7-L7). **Failure modes:** verifier bypasses abstraction in [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L23-L26). **Drill:** refactor verifier on paper.

## Pattern: BackgroundService Worker
**Problem:** Side effects should not block HTTP requests. **Real example:** [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L36). **Second:** [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6-L56). **Failure modes:** shared singleton state, crash loss. **Drill:** add durable dequeue semantics.

## Pattern: Outbox-Shaped Event
**Problem:** Side effects need retry state. **Real example:** `WebhookEvent` [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123). **Second:** enqueue/store [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L220-L264). **Failure modes:** queue and data not durable. **Drill:** write migration sketch.

## Pattern: DTO Mapping
**Problem:** API shape should not leak all domain internals. **Real example:** `InvoiceResponse.From` [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L38-L71). **Second:** money DTO mapping [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L5-L9). **Failure modes:** dashboard returns raw domain records in [Program.cs](../../../src/Billing.Api/Program.cs#L53-L62). **Drill:** create `CustomerResponse`.

## Pattern: Typed Fetch Wrapper
**Problem:** UI actions need consistent errors and headers. **Real example:** [api.ts](../../../src/Billing.Web/src/api.ts#L145-L184). **Second:** action wrapper [App.tsx](../../../src/Billing.Web/src/App.tsx#L73-L84). **Failure modes:** no runtime validation. **Drill:** add schema validation plan.

## Pattern: Derived View Lookups
**Problem:** Rendering repeated IDs should not repeatedly search arrays. **Real example:** `useMemo` maps [App.tsx](../../../src/Billing.Web/src/App.tsx#L49-L51). **Second:** generic `indexBy` [App.tsx](../../../src/Billing.Web/src/App.tsx#L386-L388). **Failure modes:** stale memo if dependencies wrong. **Drill:** intentionally remove dependency and explain bug.

## Pattern: Status Badge Styling
**Problem:** Domain statuses need visual scanning. **Real example:** status component [App.tsx](../../../src/Billing.Web/src/App.tsx#L382-L383). **Second:** CSS classes [styles.css](../../../src/Billing.Web/src/styles.css#L198-L222). **Failure modes:** new enum without CSS style. **Drill:** add a hypothetical `Refunded`.

## Pattern: Seed Data For Learning
**Problem:** Fresh app needs useful sample state. **Real example:** [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L323). **Second:** dashboard reads it [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63). **Failure modes:** demo data in production. **Drill:** move seed behind development flag.

