namespace Billing.Api.Infrastructure;

// Intentionally empty.
//
// No HTTP delivery is performed anywhere in this build. BillingEngine.Process marks
// durable outbox events Simulated in the same transaction that completes the payment.
// A real sender needs a separate design for leases, timeouts, bounded retries,
// destination validation and uncertain outcomes - that is Exercise 7.
//
// The batch limit that used to sit on a type called WebhookDeliveryWorker is now
// BillingLimits.WebhookBatchSize.
