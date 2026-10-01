using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

/// <summary>Every operation runs inside its caller's single SQLite transaction.</summary>
public sealed class BillingEngine
{
    public object RunCycle(BillingSession db, Actor actor, CycleInput input)
    {
        var w = actor.WorkspaceId; var now = CatalogService.BillingTime(db, w);
        var target = input.BillingDate.ToUniversalTime();
        if (target < now || target > now.AddDays(366) || target.Year > 2199)
            throw BillingError.Invalid("Advance the simulator monotonically by at most 366 days, before year 2200.");
        var invoices = new List<Invoice>();
        foreach (var initial in db.All<Subscription>(w).Where(s => s.Status != SubscriptionStatus.Canceled))
        {
            var subscription = initial; var plan = db.Get<Plan>(w, subscription.PlanId);
            while (subscription.CurrentPeriodEnd <= target && subscription.Status != SubscriptionStatus.Canceled)
            {
                if (invoices.Count >= 200) throw BillingError.Conflict("A cycle can create at most 200 invoices. Advance by a smaller interval.");
                var usage = db.All<UsageEvent>(w).Where(u => u.SubscriptionId == subscription.Id && u.InvoiceId is null &&
                    u.OccurredAt >= subscription.CurrentPeriodStart && u.OccurredAt < subscription.CurrentPeriodEnd).ToList();
                var quantity = usage.Sum(u => (long)u.Quantity);
                var billable = Math.Max(0L, quantity - plan.IncludedUnits);
                if (billable > int.MaxValue) throw BillingError.Conflict("This period exceeds the workshop's supported billable quantity.");
                var lines = new List<InvoiceLine>();
                if (plan.PricingModel is PlanPricingModel.Flat or PlanPricingModel.Hybrid)
                    lines.Add(new InvoiceLine(plan.Name + " base fee", 1, plan.BasePrice, plan.BasePrice, "base"));
                if (plan.PricingModel is PlanPricingModel.Metered or PlanPricingModel.Hybrid)
                    lines.Add(new InvoiceLine(plan.Metric + " above included units", (int)billable, plan.UnitPrice, plan.UnitPrice.Multiply(billable), "usage"));
                var subtotal = lines.Aggregate(Money.Zero(plan.BasePrice.Currency), (sum, line) => sum.Add(line.Amount));
                // Archive blocks new agreements, but does not revoke existing discount terms.
                var discount = subscription.CouponId is null ? Money.Zero(subtotal.Currency) :
                    db.Get<Coupon>(w, subscription.CouponId.Value).CalculateDiscount(subtotal, subscription.CurrentPeriodStart);
                var total = subtotal.Subtract(discount); var id = Guid.NewGuid();
                var invoice = new Invoice(id, "INV-" + id.ToString("N")[..12].ToUpperInvariant(), subscription.CustomerId,
                    subscription.Id, total.MinorUnits == 0 ? InvoiceStatus.Paid : InvoiceStatus.Open, lines, subtotal, discount, total,
                    subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.CurrentPeriodEnd.AddDays(14),
                    DateTimeOffset.UtcNow, total.MinorUnits == 0 ? target : null) { WorkspaceId = w };
                db.Insert(invoice); db.Link<Invoice, Subscription>(w, id, subscription.Id); db.Link<Invoice, Customer>(w, id, subscription.CustomerId);
                foreach (var item in usage)
                {
                    db.Update(item with { InvoiceId = id, Version = item.Version + 1 }, item.Version);
                    db.Link<UsageEvent, Invoice>(w, item.Id, id);
                }
                var updated = subscription with
                {
                    CurrentPeriodStart = subscription.CurrentPeriodEnd,
                    CurrentPeriodEnd = BillingCalendar.Next(subscription.CurrentPeriodEnd, plan.Cycle, subscription.AnchorDay),
                    Status = subscription.CancelAtPeriodEnd ? SubscriptionStatus.Canceled : subscription.Status,
                    Version = subscription.Version + 1
                };
                db.Update(updated, subscription.Version); subscription = updated;
                EnqueueEvent(db, w, "invoice.created", invoice); invoices.Add(invoice);
            }
        }
        db.Execute("UPDATE workspaces SET billing_time=$time WHERE id=$w", ("$time", target.ToString("O")), ("$w", w.ToString()));
        return new { billingTime = target, invoicesCreated = invoices.Count, invoiceIds = invoices.Select(i => i.Id).ToArray() };
    }

    public Payment QueuePayment(BillingSession db, Guid w, Guid invoiceId, long version)
    {
        var invoice = db.Get<Invoice>(w, invoiceId); CatalogService.CheckVersion(invoice, version);
        if (invoice.Status != InvoiceStatus.Open) throw BillingError.Conflict("Only an open invoice can be paid.");
        if (db.All<Payment>(w).Any(p => p.InvoiceId == invoiceId && p.Status == PaymentStatus.Pending))
            throw BillingError.Conflict("A payment is already pending for this invoice. Process the simulation first.");
        var now = DateTimeOffset.UtcNow;
        var payment = new Payment(Guid.NewGuid(), invoiceId, PaymentStatus.Pending, invoice.Total, "local-simulator", null, null, now, null) { WorkspaceId = w };
        var job = new PaymentJob(Guid.NewGuid(), invoiceId, 1, now, now) { WorkspaceId = w, PaymentId = payment.Id };
        db.Insert(payment); db.Link<Payment, Invoice>(w, payment.Id, invoiceId);
        db.Insert(job); db.Link<PaymentJob, Payment>(w, job.Id, payment.Id);
        db.Update(invoice with { Version = invoice.Version + 1 }, invoice.Version);
        return payment;
    }

    public Payment CompletePayment(BillingSession db, Guid w, Guid invoiceId, Guid paymentId, bool succeeded, string reference)
    {
        var payment = db.Get<Payment>(w, paymentId); var invoice = db.Get<Invoice>(w, invoiceId);
        if (payment.InvoiceId != invoiceId) throw BillingError.Invalid("Payment and invoice do not match.");
        if (payment.Status != PaymentStatus.Pending)
        {
            if ((payment.Status == PaymentStatus.Succeeded) != succeeded)
                throw BillingError.Conflict("A terminal payment attempt cannot change its result.");
            return payment;
        }
        if (invoice.Status != InvoiceStatus.Open) throw BillingError.Conflict("This invoice cannot accept another payment result.");
        var now = DateTimeOffset.UtcNow;
        var result = payment with { Status = succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            ProviderReference = Input.Text(reference, "Provider reference", 120), FailureReason = succeeded ? null : "Simulated decline",
            CompletedAt = now, Version = payment.Version + 1 };
        db.Update(result, payment.Version);
        db.Update(invoice with { Status = succeeded ? InvoiceStatus.Paid : InvoiceStatus.Open,
            PaidAt = succeeded ? now : null, Version = invoice.Version + 1 }, invoice.Version);
        foreach (var job in db.All<PaymentJob>(w).Where(j => j.PaymentId == paymentId)) db.Delete<PaymentJob>(w, job.Id, job.Version);
        EnqueueEvent(db, w, succeeded ? "invoice.paid" : "payment.failed", result);
        return result;
    }

    public object Process(BillingSession db, Guid w, ProcessInput input)
    {
        // Explicit processing makes the failure lab reproducible. No network requests occur.
        var jobs = db.All<PaymentJob>(w).Where(j => j.AvailableAt <= DateTimeOffset.UtcNow).OrderBy(j => j.AvailableAt).ThenBy(j => j.Id).Take(BillingLimits.PaymentBatchSize).ToList();
        foreach (var job in jobs) CompletePayment(db, w, job.InvoiceId, job.PaymentId, input.Succeeded, "sim-" + job.PaymentId.ToString("N"));
        var events = db.All<WebhookEvent>(w).Where(e => e.Status == WebhookDeliveryStatus.Pending && e.AvailableAt <= DateTimeOffset.UtcNow).OrderBy(e => e.AvailableAt).ThenBy(e => e.Id).Take(BillingLimits.WebhookBatchSize).ToList();
        foreach (var item in events) db.Update(item with { Status = WebhookDeliveryStatus.Simulated, Attempt = item.Attempt + 1,
            DeliveredAt = DateTimeOffset.UtcNow, LastError = null, Version = item.Version + 1 }, item.Version);
        return new { paymentsProcessed = jobs.Count, webhooksSimulated = events.Count, externalRequests = 0 };
    }

    public Invoice Void(BillingSession db, Guid w, Guid id, long version)
    {
        var invoice = db.Get<Invoice>(w, id); CatalogService.CheckVersion(invoice, version);
        if (invoice.Status != InvoiceStatus.Open || db.All<Payment>(w).Any(p => p.InvoiceId == id && p.Status == PaymentStatus.Pending))
            throw BillingError.Conflict("Only an open invoice without a pending payment can be voided.");
        var result = invoice with { Status = InvoiceStatus.Void, Version = invoice.Version + 1 };
        db.Update(result, invoice.Version); EnqueueEvent(db, w, "invoice.voided", result); return result;
    }

    private static void EnqueueEvent<T>(BillingSession db, Guid w, string type, T payload)
    {
        var now = DateTimeOffset.UtcNow;
        db.Insert(new WebhookEvent(Guid.NewGuid(), type, "local-simulator (no HTTP)", BillingJson.Write(payload),
            WebhookDeliveryStatus.Pending, 0, now, now, null, null) { WorkspaceId = w });
    }
}
