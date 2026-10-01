namespace Billing.Api.Infrastructure;

// Batch limits for the explicit simulator pass in BillingEngine.Process.
//
// These used to live on types called PaymentWorker and WebhookDeliveryWorker, which
// promised background services that this build does not have: processing is explicit
// through POST /api/simulator/process so a learner can inspect Pending state, replay a
// request, and choose a reproducible success or decline. Naming a constant holder after
// a worker that does not exist sends anyone grepping for the worker to the wrong place,
// so the constants live here and say what they are.
//
// The original hosted workers are preserved verbatim under docs/original for comparison.
public static class BillingLimits
{
    /// <summary>Maximum payment jobs completed in one explicit simulator pass.</summary>
    public const int PaymentBatchSize = 100;

    /// <summary>Maximum pending outbox events marked Simulated in one explicit simulator pass.</summary>
    public const int WebhookBatchSize = 200;
}
