using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

public sealed class BillingStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Customer> _customers = [];
    private readonly Dictionary<Guid, Plan> _plans = [];
    private readonly Dictionary<Guid, Coupon> _coupons = [];
    private readonly Dictionary<Guid, Subscription> _subscriptions = [];
    private readonly Dictionary<Guid, UsageEvent> _usageEvents = [];
    private readonly Dictionary<Guid, Invoice> _invoices = [];
    private readonly Dictionary<Guid, Payment> _payments = [];
    private readonly Queue<PaymentJob> _paymentJobs = [];
    private readonly Dictionary<Guid, WebhookEvent> _webhookEvents = [];
    private readonly Queue<Guid> _webhookQueue = [];

    public BillingStore(IBillingClock clock)
    {
        Seed(clock.UtcNow);
    }

    public IReadOnlyList<Customer> Customers()
    {
        lock (_gate) return _customers.Values.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public IReadOnlyList<Plan> Plans()
    {
        lock (_gate) return _plans.Values.OrderBy(x => x.Code).ToArray();
    }

    public IReadOnlyList<Coupon> Coupons()
    {
        lock (_gate) return _coupons.Values.OrderBy(x => x.Code).ToArray();
    }

    public IReadOnlyList<Subscription> Subscriptions()
    {
        lock (_gate) return _subscriptions.Values.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public IReadOnlyList<UsageEvent> UsageEvents()
    {
        lock (_gate) return _usageEvents.Values.OrderByDescending(x => x.OccurredAt).ToArray();
    }

    public IReadOnlyList<Invoice> Invoices()
    {
        lock (_gate) return _invoices.Values.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public IReadOnlyList<Payment> Payments()
    {
        lock (_gate) return _payments.Values.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public IReadOnlyList<WebhookEvent> WebhookEvents()
    {
        lock (_gate) return _webhookEvents.Values.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public Customer AddCustomer(Customer customer)
    {
        lock (_gate)
        {
            if (_customers.Values.Any(x => string.Equals(x.Email, customer.Email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A customer with this email already exists.");
            }

            _customers.Add(customer.Id, customer);
            return customer;
        }
    }

    public Plan AddPlan(Plan plan)
    {
        lock (_gate)
        {
            if (_plans.Values.Any(x => string.Equals(x.Code, plan.Code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A plan with this code already exists.");
            }

            _plans.Add(plan.Id, plan);
            return plan;
        }
    }

    public Coupon AddCoupon(Coupon coupon)
    {
        lock (_gate)
        {
            if (_coupons.Values.Any(x => string.Equals(x.Code, coupon.Code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A coupon with this code already exists.");
            }

            _coupons.Add(coupon.Id, coupon);
            return coupon;
        }
    }

    public Subscription AddSubscription(Subscription subscription)
    {
        lock (_gate)
        {
            EnsureExists(_customers, subscription.CustomerId, "customer");
            EnsureExists(_plans, subscription.PlanId, "plan");
            if (subscription.CouponId is Guid couponId)
            {
                EnsureExists(_coupons, couponId, "coupon");
            }

            _subscriptions.Add(subscription.Id, subscription);
            return subscription;
        }
    }

    public UsageEvent AddUsageEvent(UsageEvent usageEvent)
    {
        lock (_gate)
        {
            EnsureExists(_subscriptions, usageEvent.SubscriptionId, "subscription");
            _usageEvents.Add(usageEvent.Id, usageEvent);
            return usageEvent;
        }
    }

    public bool TryGetCustomer(Guid id, out Customer customer)
    {
        lock (_gate) return _customers.TryGetValue(id, out customer!);
    }

    public bool TryGetPlan(Guid id, out Plan plan)
    {
        lock (_gate) return _plans.TryGetValue(id, out plan!);
    }

    public bool TryGetCoupon(Guid id, out Coupon coupon)
    {
        lock (_gate) return _coupons.TryGetValue(id, out coupon!);
    }

    public bool TryGetSubscription(Guid id, out Subscription subscription)
    {
        lock (_gate) return _subscriptions.TryGetValue(id, out subscription!);
    }

    public bool TryGetInvoice(Guid id, out Invoice invoice)
    {
        lock (_gate) return _invoices.TryGetValue(id, out invoice!);
    }

    public void SaveInvoice(Invoice invoice)
    {
        lock (_gate) _invoices[invoice.Id] = invoice;
    }

    public void SaveSubscription(Subscription subscription)
    {
        lock (_gate) _subscriptions[subscription.Id] = subscription;
    }

    public void SaveUsageEvents(IEnumerable<UsageEvent> usageEvents)
    {
        lock (_gate)
        {
            foreach (var usageEvent in usageEvents)
            {
                _usageEvents[usageEvent.Id] = usageEvent;
            }
        }
    }

    public IReadOnlyList<UsageEvent> GetUnbilledUsage(Guid subscriptionId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        lock (_gate)
        {
            return _usageEvents.Values
                .Where(x => x.SubscriptionId == subscriptionId &&
                            x.InvoiceId is null &&
                            x.OccurredAt >= periodStart &&
                            x.OccurredAt < periodEnd)
                .OrderBy(x => x.OccurredAt)
                .ToArray();
        }
    }

    public void EnqueuePaymentJob(Guid invoiceId, DateTimeOffset availableAt)
    {
        lock (_gate)
        {
            _paymentJobs.Enqueue(new PaymentJob(Guid.NewGuid(), invoiceId, 0, availableAt, availableAt));
        }
    }

    public bool TryDequeuePaymentJob(DateTimeOffset now, out PaymentJob job)
    {
        lock (_gate)
        {
            if (_paymentJobs.Count > 0 && _paymentJobs.Peek().AvailableAt <= now)
            {
                job = _paymentJobs.Dequeue();
                return true;
            }

            job = default!;
            return false;
        }
    }

    public void SavePayment(Payment payment)
    {
        lock (_gate) _payments[payment.Id] = payment;
    }

    public void EnqueueWebhook(WebhookEvent webhookEvent)
    {
        lock (_gate)
        {
            _webhookEvents[webhookEvent.Id] = webhookEvent;
            _webhookQueue.Enqueue(webhookEvent.Id);
        }
    }

    public bool TryDequeueWebhook(DateTimeOffset now, out WebhookEvent webhookEvent)
    {
        lock (_gate)
        {
            while (_webhookQueue.Count > 0)
            {
                var id = _webhookQueue.Peek();
                if (!_webhookEvents.TryGetValue(id, out var candidate))
                {
                    _webhookQueue.Dequeue();
                    continue;
                }

                if (candidate.AvailableAt <= now)
                {
                    _webhookQueue.Dequeue();
                    webhookEvent = candidate;
                    return true;
                }

                break;
            }

            webhookEvent = default!;
            return false;
        }
    }

    public void SaveWebhook(WebhookEvent webhookEvent)
    {
        lock (_gate) _webhookEvents[webhookEvent.Id] = webhookEvent;
    }

    public void RequeueWebhook(Guid webhookEventId)
    {
        lock (_gate) _webhookQueue.Enqueue(webhookEventId);
    }

    private void Seed(DateTimeOffset now)
    {
        var starter = new Plan(
            Guid.Parse("7ec9eb71-7dd0-461f-b4d2-26772723fb49"),
            "starter",
            "Starter",
            BillingCycle.Monthly,
            PlanPricingModel.Flat,
            Money.FromMajor(29m, "USD"),
            0,
            Money.Zero("USD"),
            true,
            now.AddDays(-90));

        var scale = new Plan(
            Guid.Parse("21bde235-8921-41ed-9855-b04235bbdb80"),
            "scale",
            "Scale Usage",
            BillingCycle.Monthly,
            PlanPricingModel.Hybrid,
            Money.FromMajor(249m, "USD"),
            100_000,
            Money.FromMajor(0.01m, "USD"),
            true,
            now.AddDays(-70));

        var coupon = new Coupon(
            Guid.Parse("8e780f58-2fe4-4bea-9d47-a2aef378e053"),
            "LAUNCH20",
            "Launch 20%",
            20m,
            null,
            now.AddMonths(3),
            now.AddDays(-45));

        var customerA = new Customer(Guid.Parse("d7ebbcee-36c5-4268-9e6a-dd67c7df056f"), "Acme Analytics", "billing@acme.example", "USD", now.AddDays(-42));
        var customerB = new Customer(Guid.Parse("02d6cff9-d49a-4142-8ab9-02306019d4f5"), "Northstar Labs", "finance@northstar.example", "USD", now.AddDays(-25));

        var subA = new Subscription(Guid.Parse("062a6a8f-9afe-446f-8e7f-dbc4e7a20f2d"), customerA.Id, scale.Id, coupon.Id, SubscriptionStatus.Active, now.AddDays(-30), now.AddDays(1), now.AddDays(-42));
        var subB = new Subscription(Guid.Parse("86852b42-7d08-4db9-9343-bb84f014641d"), customerB.Id, starter.Id, null, SubscriptionStatus.Active, now.AddDays(-15), now.AddDays(15), now.AddDays(-25));

        _plans[starter.Id] = starter;
        _plans[scale.Id] = scale;
        _coupons[coupon.Id] = coupon;
        _customers[customerA.Id] = customerA;
        _customers[customerB.Id] = customerB;
        _subscriptions[subA.Id] = subA;
        _subscriptions[subB.Id] = subB;

        for (var i = 0; i < 8; i++)
        {
            var usage = new UsageEvent(Guid.NewGuid(), subA.Id, 18_000 + i * 2_500, "api_calls", now.AddDays(-20 + i * 2), null, now.AddDays(-20 + i * 2));
            _usageEvents[usage.Id] = usage;
        }
    }

    private static void EnsureExists<T>(IReadOnlyDictionary<Guid, T> values, Guid id, string resourceName)
    {
        if (!values.ContainsKey(id))
        {
            throw new KeyNotFoundException($"Unknown {resourceName}: {id}.");
        }
    }
}
