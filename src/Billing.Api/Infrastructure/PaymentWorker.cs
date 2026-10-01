namespace Billing.Api.Infrastructure;

// Intentionally empty.
//
// There is no payment worker in this build. Payment processing is explicit through
// POST /api/simulator/process (see BillingEngine.Process) so learners can inspect
// Pending state, replay requests, and choose a reproducible success or decline.
// The batch limit that used to sit on a type called PaymentWorker is now
// BillingLimits.PaymentBatchSize, which does not promise a background service.
//
// The original hosted worker is preserved verbatim in docs/original for comparison;
// re-introducing it is Exercise 7.
