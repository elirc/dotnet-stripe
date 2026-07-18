using Billing.Api.Domain;
using Microsoft.Extensions.Options;

namespace Billing.Api.Infrastructure;

public sealed class WebhookDeliveryWorker(
    BillingStore store,
    IBillingClock clock,
    IOptions<BillingOptions> options,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.WebhookWorkerIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            while (store.TryDequeueWebhook(clock.UtcNow, out var webhookEvent))
            {
                Deliver(webhookEvent);
            }
        }
    }

    private void Deliver(WebhookEvent webhookEvent)
    {
        try
        {
            var delivered = webhookEvent with
            {
                Status = WebhookDeliveryStatus.Succeeded,
                Attempt = webhookEvent.Attempt + 1,
                DeliveredAt = clock.UtcNow,
                LastError = "Simulated delivery. Replace with durable HTTP delivery in production."
            };

            store.SaveWebhook(delivered);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook {WebhookEventId} delivery failed.", webhookEvent.Id);
            var nextAttempt = webhookEvent.Attempt + 1;
            var failed = webhookEvent with
            {
                Status = WebhookDeliveryStatus.Failed,
                Attempt = nextAttempt,
                AvailableAt = clock.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, nextAttempt))),
                LastError = ex.Message
            };
            store.SaveWebhook(failed);
            store.RequeueWebhook(failed.Id);
        }
    }
}

