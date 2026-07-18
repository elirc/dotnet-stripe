using System.Text.Json;
using Billing.Api.Contracts;
using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

public sealed class BillingEngine(BillingStore store, IBillingClock clock)
{
    public Invoice GenerateInvoice(Guid subscriptionId, DateTimeOffset billingDate)
    {
        if (!store.TryGetSubscription(subscriptionId, out var subscription))
        {
            throw new KeyNotFoundException($"Unknown subscription: {subscriptionId}.");
        }

        if (subscription.Status is SubscriptionStatus.Canceled)
        {
            throw new InvalidOperationException("Canceled subscriptions cannot be invoiced.");
        }

        if (!store.TryGetCustomer(subscription.CustomerId, out var customer) ||
            !store.TryGetPlan(subscription.PlanId, out var plan))
        {
            throw new InvalidOperationException("Subscription references missing customer or plan.");
        }

        Coupon? coupon = null;
        if (subscription.CouponId is Guid couponId && store.TryGetCoupon(couponId, out var foundCoupon))
        {
            coupon = foundCoupon;
        }

        var lines = new List<InvoiceLine>();
        var currency = customer.Currency;

        if (plan.PricingModel is PlanPricingModel.Flat or PlanPricingModel.Hybrid)
        {
            lines.Add(new InvoiceLine(
                $"{plan.Name} {plan.Cycle.ToString().ToLowerInvariant()} subscription",
                1,
                plan.BasePrice,
                plan.BasePrice,
                "recurring"));
        }

        var unbilledUsage = store.GetUnbilledUsage(subscription.Id, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd);
        if (plan.PricingModel is PlanPricingModel.Metered or PlanPricingModel.Hybrid)
        {
            var totalUsage = unbilledUsage.Sum(x => x.Quantity);
            var billableUnits = Math.Max(0, totalUsage - plan.IncludedUnits);
            if (billableUnits > 0)
            {
                lines.Add(new InvoiceLine(
                    $"{billableUnits:N0} billable {unbilledUsage.FirstOrDefault()?.Metric ?? "usage"} units",
                    billableUnits,
                    plan.UnitPrice,
                    plan.UnitPrice.Multiply(billableUnits),
                    "usage"));
            }
        }

        var subtotal = lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.Amount));
        var discount = coupon?.CalculateDiscount(subtotal, billingDate) ?? Money.Zero(currency);
        var total = subtotal.Subtract(discount).Max(Money.Zero(currency));
        var invoiceId = Guid.NewGuid();
        var invoice = new Invoice(
            invoiceId,
            InvoiceNumber(invoiceId, billingDate),
            customer.Id,
            subscription.Id,
            InvoiceStatus.Open,
            lines,
            subtotal,
            discount,
            total,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            billingDate.AddDays(14),
            clock.UtcNow,
            null);

        store.SaveInvoice(invoice);
        store.SaveUsageEvents(unbilledUsage.Select(x => x with { InvoiceId = invoice.Id }));
        store.SaveSubscription(subscription with
        {
            CurrentPeriodStart = subscription.CurrentPeriodEnd,
            CurrentPeriodEnd = NextPeriodEnd(subscription.CurrentPeriodEnd, plan.Cycle)
        });

        var payload = JsonSerializer.Serialize(new
        {
            type = "invoice.created",
            invoice.Id,
            invoice.Number,
            invoice.Total,
            invoice.CustomerId,
            invoice.SubscriptionId
        });

        store.EnqueueWebhook(new WebhookEvent(
            Guid.NewGuid(),
            "invoice.created",
            "https://example.com/webhooks/billing",
            payload,
            WebhookDeliveryStatus.Pending,
            0,
            clock.UtcNow,
            clock.UtcNow,
            null,
            null));

        return invoice;
    }

    public IReadOnlyList<Invoice> RunBillingCycle(DateTimeOffset billingDate)
    {
        var generated = new List<Invoice>();
        foreach (var subscription in store.Subscriptions().Where(x => x.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing))
        {
            if (subscription.CurrentPeriodEnd <= billingDate)
            {
                generated.Add(GenerateInvoice(subscription.Id, billingDate));
            }
        }

        return generated;
    }

    public Payment QueuePayment(Guid invoiceId, string provider)
    {
        if (!store.TryGetInvoice(invoiceId, out var invoice))
        {
            throw new KeyNotFoundException($"Unknown invoice: {invoiceId}.");
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Invoice is already paid.");
        }

        var payment = new Payment(
            Guid.NewGuid(),
            invoice.Id,
            PaymentStatus.Pending,
            invoice.Total,
            string.IsNullOrWhiteSpace(provider) ? "simulated" : provider.Trim(),
            null,
            null,
            clock.UtcNow,
            null);

        store.SavePayment(payment);
        store.EnqueuePaymentJob(invoice.Id, clock.UtcNow);
        return payment;
    }

    public void ApplyPaymentResult(Guid invoiceId, bool succeeded, string? providerReference, string? failureReason)
    {
        if (!store.TryGetInvoice(invoiceId, out var invoice))
        {
            throw new KeyNotFoundException($"Unknown invoice: {invoiceId}.");
        }

        var now = clock.UtcNow;
        var updatedInvoice = succeeded
            ? invoice with { Status = InvoiceStatus.Paid, PaidAt = now }
            : invoice with { Status = InvoiceStatus.Open };

        store.SaveInvoice(updatedInvoice);
        store.SavePayment(new Payment(
            Guid.NewGuid(),
            invoice.Id,
            succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            invoice.Total,
            "provider-webhook",
            providerReference,
            failureReason,
            now,
            now));

        store.EnqueueWebhook(new WebhookEvent(
            Guid.NewGuid(),
            succeeded ? "invoice.payment_succeeded" : "invoice.payment_failed",
            "https://example.com/webhooks/billing",
            JsonSerializer.Serialize(new
            {
                type = succeeded ? "invoice.payment_succeeded" : "invoice.payment_failed",
                invoice.Id,
                invoice.Number,
                invoice.Total,
                providerReference,
                failureReason
            }),
            WebhookDeliveryStatus.Pending,
            0,
            now,
            now,
            null,
            null));
    }

    private static DateTimeOffset NextPeriodEnd(DateTimeOffset currentPeriodEnd, BillingCycle cycle) =>
        cycle == BillingCycle.Annual ? currentPeriodEnd.AddYears(1) : currentPeriodEnd.AddMonths(1);

    private static string InvoiceNumber(Guid invoiceId, DateTimeOffset billingDate) =>
        $"INV-{billingDate:yyyyMM}-{invoiceId.ToString("N")[..8].ToUpperInvariant()}";
}

