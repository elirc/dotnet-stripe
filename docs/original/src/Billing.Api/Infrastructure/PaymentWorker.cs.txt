using Billing.Api.Domain;
using Microsoft.Extensions.Options;

namespace Billing.Api.Infrastructure;

public sealed class PaymentWorker(
    BillingStore store,
    BillingEngine engine,
    IBillingClock clock,
    IOptions<BillingOptions> options,
    ILogger<PaymentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PaymentWorkerIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            while (store.TryDequeuePaymentJob(clock.UtcNow, out var job))
            {
                Process(job);
            }
        }
    }

    private void Process(PaymentJob job)
    {
        if (!store.TryGetInvoice(job.InvoiceId, out var invoice))
        {
            logger.LogWarning("Dropping payment job {PaymentJobId}; invoice {InvoiceId} was not found.", job.Id, job.InvoiceId);
            return;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return;
        }

        engine.ApplyPaymentResult(
            invoice.Id,
            succeeded: true,
            providerReference: $"pay_{Guid.NewGuid():N}",
            failureReason: null);
    }
}

