# Annotation Drills

For each drill, annotate inputs, outputs, dependencies, invariants, side effects, failure modes, and one useful test.

1. **Money arithmetic:** [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L12-L57). Focus on rounding, currency equality, and why `MinorUnits` matters.
2. **Coupon discount cap:** [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L31-L48). Explain percent plus amount and subtotal cap.
3. **Create customer endpoint:** [Program.cs](../../../src/Billing.Api/Program.cs#L67-L83) plus [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76). Separate HTTP validation from invariant.
4. **Plan creation:** [Program.cs](../../../src/Billing.Api/Program.cs#L87-L120). Track decimal input into `Money.FromMajor`.
5. **Subscription creation:** [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174) plus [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L106-L119). Identify missing duplicate-subscription rule.
6. **Usage event:** [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202). Identify default metric and quantity validation.
7. **Invoice generation:** [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L105). Mark all writes and what should be transactional.
8. **Payment queue:** [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L154) plus [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L35). Explain eventual consistency.
9. **Idempotency middleware:** [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9-L88). Explain body buffering and conflict detection.
10. **Webhook signature:** [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L38). Explain timestamp tolerance and fixed-time compare.
11. **React action wrapper:** [App.tsx](../../../src/Billing.Web/src/App.tsx#L64-L84). Explain busy/error lifecycle.
12. **Typed fetch wrapper:** [api.ts](../../../src/Billing.Web/src/api.ts#L149-L184). Explain error propagation and idempotency key generation.

## Rubric

Strong annotations use precise terms: invariant, boundary, contract, side effect, consistency, idempotency, authorization, latency, observability, blast radius.

