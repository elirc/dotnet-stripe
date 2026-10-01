> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/01-codebase-cartography/05-key-flows.md.txt).

# Key Flows

## Flow: Dashboard Load

**Why it matters:** This is the browser-to-API read path and the learner’s first contract trace.
**Open first:** [App.tsx](../../../src/Billing.Web/src/App.tsx#L53-L71) because it loads data through `refresh`.

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | React | [App.tsx](../../../src/Billing.Web/src/App.tsx#L53-L71) | `useEffect` calls `refresh`. | `DashboardResponse \| null` | No cancellation if component unmounts. |
| 2 | API client | [api.ts](../../../src/Billing.Web/src/api.ts#L145-L168) | `GET /api/dashboard`. | typed promise | Runtime JSON is trusted. |
| 3 | API | [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63) | Reads store slices and metrics. | customers/plans/invoices/etc. | Aggregate may grow unbounded. |
| 4 | Metrics | [Program.cs](../../../src/Billing.Api/Program.cs#L254-L290) | Calculates MRR/outstanding/paid rate. | `DashboardMetrics` | USD assumption in [Program.cs](../../../src/Billing.Api/Program.cs#L262-L278). |

**Validation & authorization:** No auth boundary is configured near [Program.cs](../../../src/Billing.Api/Program.cs#L21-L35); investigate before real deployment.  
**Persistence & side effects:** Store read methods return arrays in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L24-L62).  
**Tests:** No direct coverage found; add WebApplicationFactory tests around [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63).  
**Juniors miss:** reads need performance budgets too. **Seniors notice:** aggregate endpoints need pagination or projection as data grows.  
**Drill:** Sketch the response shape from [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L5-L71). **Self-grade:** Basic names files; Solid explains data shape; Strong identifies growth risk.

## Flow: Create Customer

**Why it matters:** Smallest write path: form, idempotent POST, endpoint validation, store invariant.
**Open first:** [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L92).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | React | [App.tsx](../../../src/Billing.Web/src/App.tsx#L86-L92) | Form posts `customerForm`. | name/email/currency | Client validation is not security. |
| 2 | API client | [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158) | Adds `Idempotency-Key`. | JSON body | Random key means manual retry of same action will not replay. |
| 3 | Middleware | [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L57) | Replays or rejects conflicting key/body. | cached response | In-memory cache is process-local. |
| 4 | API | [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83) | Validates name/email and creates record. | `Customer` | Email format not validated server-side. |
| 5 | Store | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76) | Rejects duplicate email. | dictionary write | Case-insensitive but not tenant-scoped. |

**Tests:** Add duplicate email test around [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76).  
**Drill:** Explain why the endpoint and store both validate different things. **Self-grade:** Strong answer uses “boundary” and “invariant.”

## Flow: Create Subscription

**Why it matters:** Cross-aggregate write: customer, plan, optional coupon, billing period.
**Open first:** [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | React | [App.tsx](../../../src/Billing.Web/src/App.tsx#L122-L130) | Sends selected IDs. | GUID strings | UI assumes loaded options exist. |
| 2 | API | [Program.cs](../../../src/Billing.Api/Program.cs#L155-L164) | Finds plan and computes period. | subscription draft | Customer not checked until store call. |
| 3 | Store | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L106-L119) | Ensures customer/plan/coupon exist. | subscription | No uniqueness rule for duplicate active subscriptions. |

**Validation & authorization:** No current user or tenant check exists near [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174).  
**Persistence:** In-memory dictionary add in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L117-L117).  
**Drill:** Propose an invariant: one active subscription per customer-plan pair.

## Flow: Usage-Based Billing Cycle

**Why it matters:** Core billing behavior with metered usage, discounts, invoices, and period rollover.
**Open first:** [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L105).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [App.tsx](../../../src/Billing.Web/src/App.tsx#L145-L149) | Posts billing date 35 days ahead. | ISO date | Simulator behavior can hide real period rules. |
| 2 | API | [Program.cs](../../../src/Billing.Api/Program.cs#L204-L209) | Calls `RunBillingCycle`. | generated invoices | No cancellation token. |
| 3 | Engine | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124) | Finds subscriptions due by date. | subscriptions | Race if two workers run. |
| 4 | Usage | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L44-L60) | Calculates billable units beyond included units. | usage line | Sub-cent pricing not supported by `Money`. |
| 5 | Totals | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L81) | Applies discount and due date. | invoice | Need auditability when coupons change. |
| 6 | Side effects | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105) | Saves invoice, marks usage, rolls period, enqueues webhook. | multiple writes | No transaction in in-memory store. |

**Tests:** No direct coverage found. Test discount cap with [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L31-L48), then integration test endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L204-L209).  
**Drill:** Trace one seeded subscription from [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L302-L319) into an invoice.

## Flow: Pay Invoice

**Why it matters:** Demonstrates eventual consistency: user queues payment, worker applies result later.
**Open first:** [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L154).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI | [App.tsx](../../../src/Billing.Web/src/App.tsx#L152-L154) | Posts pay request. | provider string | User can click before worker completes. |
| 2 | API | [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221) | Returns `202 Accepted`. | pending payment | No provider abstraction. |
| 3 | Engine | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L154) | Saves pending payment and queues job. | `PaymentJob` | Queue is in-memory. |
| 4 | Worker | [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L35) | Dequeues and applies success. | payment result | Always succeeds; failures only inbound webhook. |
| 5 | Engine | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199) | Marks invoice paid and emits webhook. | invoice/payment/webhook | Duplicate provider events need dedupe. |

**Drill:** Why is `202 Accepted` more honest than `200 Paid` here?

## Flow: Signed Provider Webhook

**Why it matters:** Webhooks are public input and need authenticity, replay control, and idempotent handling.
**Open first:** [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L7-L38).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | API | [Program.cs](../../../src/Billing.Api/Program.cs#L223-L250) | Reads raw body and verifies signature. | raw JSON | No event-id dedupe beyond generic idempotency key. |
| 2 | Verifier | [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L38) | Checks timestamp and HMAC. | signed payload | Uses system time directly. |
| 3 | Contract | [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L33-L38) | Deserializes provider payload. | event/invoice/result | No schema version. |
| 4 | Engine | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199) | Applies invoice payment result. | invoice/payment | No state transition guard for late failure after paid. |

**Security drill:** Write down what happens if a valid payment failure arrives after a success. Label as investigate, then inspect [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199).

## Flow: Outbound Webhook Delivery

**Why it matters:** Side effects should be visible and retryable.
**Open first:** [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L15-L56).

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Engine | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L100-L113) | Enqueues `invoice.created`. | `WebhookEvent` | Destination is placeholder. |
| 2 | Store | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L220-L264) | Stores and queues event IDs. | queue/dictionary | Process crash loses queue. |
| 3 | Worker | [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39) | Marks simulated success. | delivered event | No real HTTP, timeout, or signing. |
| 4 | Failure path | [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L55) | Saves error, backs off, requeues. | failed event | Not exercised by simulated success. |

**Drill:** Design the smallest real HTTP sender around this outbox model without changing the domain record.

