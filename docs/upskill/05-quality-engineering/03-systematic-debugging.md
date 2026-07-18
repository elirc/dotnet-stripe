# Systematic Debugging

Use this loop: reproduce, narrow layer, hypothesize, test cheaply, fix root cause, add regression coverage.

## Scenario: Dashboard Shows Load Failed

**Reproduction:** open client while API is not running. **First question:** browser/network or API? **Narrowing path:** check API URL in [api.ts](../../../src/Billing.Web/src/api.ts#L1-L1), fetch error handling in [api.ts](../../../src/Billing.Web/src/api.ts#L160-L177), UI banner in [App.tsx](../../../src/Billing.Web/src/App.tsx#L159-L183). **Useful probes:** browser network panel, `GET /api/health` from [Program.cs](../../../src/Billing.Api/Program.cs#L39-L39). **Regression test:** UI displays error on rejected `getDashboard`. **Senior lesson:** observable startup failures beat silent empty screens.

## Scenario: Run Cycle Generates No Invoices

**Narrowing path:** button posts future date in [App.tsx](../../../src/Billing.Web/src/App.tsx#L145-L149), endpoint passes date in [Program.cs](../../../src/Billing.Api/Program.cs#L204-L209), engine filters period end in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124). **Likely root causes:** subscription period end after billing date; subscription not active. **Regression test:** due and not-due subscriptions.

## Scenario: Duplicate Customer Error

**Narrowing path:** endpoint validation [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83), duplicate invariant [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76), client error banner [App.tsx](../../../src/Billing.Web/src/App.tsx#L73-L84). **Senior lesson:** errors from store invariants need user-friendly mapping.

## Scenario: Webhook Returns Unauthorized

**Narrowing path:** raw body reading [Program.cs](../../../src/Billing.Api/Program.cs#L223-L228), verifier headers [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L21), timestamp tolerance [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L23-L26), HMAC compare [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L28-L38). **Regression test:** valid signature, old timestamp, wrong secret.

## Scenario: Payment Never Becomes Paid

**Narrowing path:** pay endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221), queue enqueue [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L152-L153), dequeue [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L200-L213), worker processing [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L20-L35). **Useful probes:** worker logs and breakpoint in `Process`. **Regression test:** queue job processed marks invoice paid.

## Scenario: Webhook Events Stay Pending

**Narrowing path:** enqueue [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L100-L113), queue storage [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L220-L264), worker loop [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L15-L24), success mark [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L29-L39). **Senior lesson:** “pending forever” is an observability problem, not just a queue problem.

