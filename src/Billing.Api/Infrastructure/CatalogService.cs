using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

public sealed class CatalogService
{
    public static readonly string[] Kinds = ["customers", "plans", "coupons", "subscriptions", "usage-events", "invoices", "payments", "webhooks"];
    public static DateTimeOffset BillingTime(BillingSession db, Guid workspace) => DateTimeOffset.Parse(
        (string?)db.Scalar("SELECT billing_time FROM workspaces WHERE id=$w", ("$w", workspace.ToString())) ?? throw BillingError.Missing(),
        System.Globalization.CultureInfo.InvariantCulture);

    public object Page(BillingSession db, Guid workspace, string kind, int offset, int limit, string search)
    {
        if (!Kinds.Contains(kind)) throw BillingError.Missing();
        if (offset < 0 || offset > 5000 || limit < 1 || limit > 100 || search.Length > 100)
            throw BillingError.Invalid("Offset must be 0–5000, limit 1–100, and search at most 100 characters.");
        const string where = "workspace_id=$w AND kind=$k AND instr(lower(coalesce(json_extract(data,'$.name'),'') || ' ' || coalesce(json_extract(data,'$.email'),'') || ' ' || coalesce(json_extract(data,'$.code'),'') || ' ' || coalesce(json_extract(data,'$.number'),'') || ' ' || id),lower($q))>0";
        var total = Convert.ToInt64(db.Scalar("SELECT count(*) FROM records WHERE " + where,
            ("$w", workspace.ToString()), ("$k", kind), ("$q", search)));
        using var command = db.Command("SELECT data FROM records WHERE " + where + " ORDER BY json_extract(data,'$.createdAt') DESC,id LIMIT $limit OFFSET $offset",
            ("$w", workspace.ToString()), ("$k", kind), ("$q", search), ("$limit", limit), ("$offset", offset));
        using var reader = command.ExecuteReader(); var items = new List<System.Text.Json.JsonElement>();
        while (reader.Read()) items.Add(BillingJson.Read<System.Text.Json.JsonElement>(reader.GetString(0)));
        return new { items, total, offset, limit };
    }

    public IBillingEntity Get(BillingSession db, Guid w, string kind, Guid id) => kind switch
    {
        "customers" => db.Get<Customer>(w, id), "plans" => db.Get<Plan>(w, id), "coupons" => db.Get<Coupon>(w, id),
        "subscriptions" => db.Get<Subscription>(w, id), "usage-events" => db.Get<UsageEvent>(w, id),
        "invoices" => db.Get<Invoice>(w, id), "payments" => db.Get<Payment>(w, id), "webhooks" => db.Get<WebhookEvent>(w, id),
        _ => throw BillingError.Missing()
    };

    public Customer Customer(BillingSession db, Guid w, CustomerInput input, Guid? id = null, long expected = 0)
    {
        var before = id is null ? null : db.Get<Customer>(w, id.Value);
        CheckVersion(before, expected);
        var currency = Money.NormalizeCurrency(input.Currency);
        if (before is not null && before.Currency != currency && db.Referenced<Customer>(w, before.Id))
            throw BillingError.Conflict("A referenced customer's currency is immutable. Create another customer for a different currency.");
        var value = new Customer(id ?? Guid.NewGuid(), Input.Text(input.Name, "Name"), Input.Email(input.Email), currency,
            before?.CreatedAt ?? DateTimeOffset.UtcNow) { WorkspaceId = w, Version = (before?.Version ?? 0) + 1, IsArchived = input.IsArchived };
        Save(db, value, before); return value;
    }

    public Plan Plan(BillingSession db, Guid w, PlanInput input, Guid? id = null, long expected = 0)
    {
        var before = id is null ? null : db.Get<Plan>(w, id.Value); CheckVersion(before, expected);
        var cycle = Input.EnumValue(input.Cycle); var model = Input.EnumValue(input.PricingModel);
        if (input.IncludedUnits < 0 || input.IncludedUnits > 1_000_000_000 || input.BasePrice < 0 || input.UnitPrice < 0)
            throw BillingError.Invalid("Prices cannot be negative; included units must be 0–1,000,000,000.");
        if (model == PlanPricingModel.Flat && (input.IncludedUnits != 0 || input.UnitPrice != 0))
            throw BillingError.Invalid("Flat plans must have zero included units and unit price.");
        if (model == PlanPricingModel.Metered && input.BasePrice != 0)
            throw BillingError.Invalid("Metered plans must have zero base price; use Hybrid to combine prices.");
        var value = new Plan(id ?? Guid.NewGuid(), Input.Code(input.Code), Input.Text(input.Name, "Name"), cycle, model,
            Money.FromMajor(input.BasePrice, input.Currency), input.IncludedUnits, Money.FromMajor(input.UnitPrice, input.Currency),
            input.IsActive, before?.CreatedAt ?? DateTimeOffset.UtcNow)
        { WorkspaceId = w, Version = (before?.Version ?? 0) + 1, Metric = Input.Text(input.Metric, "Metric", 48) };
        if (before is not null && db.Referenced<Plan>(w, before.Id) &&
            (value.Code != before.Code || value.Cycle != before.Cycle || value.PricingModel != before.PricingModel ||
             value.BasePrice != before.BasePrice || value.UnitPrice != before.UnitPrice || value.IncludedUnits != before.IncludedUnits || value.Metric != before.Metric))
            throw BillingError.Conflict("Referenced plan prices, code, cycle and metric are immutable. Rename/archive it or create a new plan.");
        Save(db, value, before); return value;
    }

    public Coupon Coupon(BillingSession db, Guid w, CouponInput input, Guid? id = null, long expected = 0)
    {
        var before = id is null ? null : db.Get<Coupon>(w, id.Value); CheckVersion(before, expected);
        if ((input.PercentOff is null) == (input.AmountOff is null) || input.PercentOff is <= 0 or > 100 || input.AmountOff is <= 0)
            throw BillingError.Invalid("Choose exactly one discount: a percentage greater than 0 up to 100, or a positive fixed amount.");
        if (input.PercentOff is not null && decimal.Round(input.PercentOff.Value, 2) != input.PercentOff)
            throw BillingError.Invalid("Percentage must have at most two decimal places.");
        Money.NormalizeCurrency(input.Currency);
        var expiry = input.ExpiresAt?.ToUniversalTime();
        if (expiry is not null && (expiry.Value.Year < 2000 || expiry.Value.Year > 2200))
            throw BillingError.Invalid("Expiry year must be 2000–2200.");
        var value = new Coupon(id ?? Guid.NewGuid(), Input.Code(input.Code), Input.Text(input.Name, "Name"), input.PercentOff,
            input.AmountOff is null ? null : Money.FromMajor(input.AmountOff.Value, input.Currency), expiry,
            before?.CreatedAt ?? DateTimeOffset.UtcNow) { WorkspaceId = w, Version = (before?.Version ?? 0) + 1, IsActive = input.IsActive };
        if (before is not null && db.Referenced<Coupon>(w, before.Id) &&
            (value.Code != before.Code || value.PercentOff != before.PercentOff || value.AmountOff != before.AmountOff || value.ExpiresAt != before.ExpiresAt))
            throw BillingError.Conflict("Referenced discount terms are immutable. Rename/archive this coupon or create a replacement.");
        Save(db, value, before); return value;
    }

    public Subscription Subscribe(BillingSession db, Guid w, SubscriptionInput input)
    {
        var customer = db.Get<Customer>(w, input.CustomerId); var plan = db.Get<Plan>(w, input.PlanId);
        var now = BillingTime(db, w);
        if (customer.IsArchived || !plan.IsActive) throw BillingError.Conflict("Choose an active customer and plan.");
        if (customer.Currency != plan.BasePrice.Currency) throw BillingError.Invalid("Customer and plan currencies must match.");
        if (input.CouponId is not null)
        {
            var coupon = db.Get<Coupon>(w, input.CouponId.Value);
            if (!coupon.IsActive || coupon.ExpiresAt <= now) throw BillingError.Conflict("Coupon is inactive or expired.");
            if (coupon.AmountOff is not null && coupon.AmountOff.Value.Currency != customer.Currency)
                throw BillingError.Invalid("Fixed discount currency must match the customer.");
        }
        var sub = new Subscription(Guid.NewGuid(), customer.Id, plan.Id, input.CouponId, SubscriptionStatus.Active,
            now, BillingCalendar.Next(now, plan.Cycle, now.Day), DateTimeOffset.UtcNow) { WorkspaceId = w, AnchorDay = now.Day };
        db.Insert(sub); db.Link<Subscription, Customer>(w, sub.Id, customer.Id); db.Link<Subscription, Plan>(w, sub.Id, plan.Id);
        if (input.CouponId is not null) db.Link<Subscription, Coupon>(w, sub.Id, input.CouponId.Value);
        return sub;
    }

    public Subscription EditSubscription(BillingSession db, Guid w, Guid id, SubscriptionEdit input, long expected)
    {
        var before = db.Get<Subscription>(w, id); CheckVersion(before, expected);
        if (before.Status == SubscriptionStatus.Canceled) throw BillingError.Conflict("A fully canceled subscription cannot resume. Create a new subscription.");
        var value = before with { CancelAtPeriodEnd = input.CancelAtPeriodEnd, Version = before.Version + 1 };
        db.Update(value, before.Version); return value;
    }

    public UsageEvent Usage(BillingSession db, Guid w, UsageInput input, Guid? id = null, long expected = 0)
    {
        var before = id is null ? null : db.Get<UsageEvent>(w, id.Value); CheckVersion(before, expected);
        if (before?.InvoiceId is not null) throw BillingError.Conflict("Invoiced usage is immutable.");
        var subscription = db.Get<Subscription>(w, input.SubscriptionId); var plan = db.Get<Plan>(w, subscription.PlanId);
        var occurred = input.OccurredAt.ToUniversalTime();
        if (before is not null && before.SubscriptionId != input.SubscriptionId) throw BillingError.Invalid("Usage cannot move to another subscription.");
        if (subscription.Status == SubscriptionStatus.Canceled || plan.PricingModel == PlanPricingModel.Flat)
            throw BillingError.Conflict("Usage requires a current metered or hybrid subscription.");
        if (input.Quantity < 1 || input.Quantity > 1_000_000_000 || input.Metric != plan.Metric)
            throw BillingError.Invalid("Quantity must be 1–1,000,000,000 and metric must match the plan.");
        if (occurred < subscription.CurrentPeriodStart || occurred >= subscription.CurrentPeriodEnd || occurred > BillingTime(db, w))
            throw BillingError.Invalid("Usage must fall inside the current period and not after the simulator clock.");
        var value = new UsageEvent(id ?? Guid.NewGuid(), subscription.Id, input.Quantity, input.Metric, occurred, null,
            before?.CreatedAt ?? DateTimeOffset.UtcNow) { WorkspaceId = w, Version = (before?.Version ?? 0) + 1 };
        Save(db, value, before); db.Link<UsageEvent, Subscription>(w, value.Id, subscription.Id); return value;
    }

    public object Delete(BillingSession db, Guid w, string kind, Guid id, long version)
    {
        var entity = Get(db, w, kind, id); CheckVersion(entity, version);
        switch (kind)
        {
            case "customers": db.Delete<Customer>(w, id, version); break;
            case "plans": db.Delete<Plan>(w, id, version); break;
            case "coupons": db.Delete<Coupon>(w, id, version); break;
            case "subscriptions": db.Delete<Subscription>(w, id, version); break;
            case "usage-events":
                if (((UsageEvent)entity).InvoiceId is not null) throw BillingError.Conflict("Invoiced usage cannot be deleted.");
                db.Delete<UsageEvent>(w, id, version); break;
            default: throw BillingError.Conflict("Financial history cannot be deleted. Void an eligible invoice instead.");
        }
        return new { id, deleted = true };
    }

    public static void CheckVersion(IBillingEntity? before, long expected)
    {
        if (before is not null && before.Version != expected) throw BillingError.Conflict("This record changed. Reload and review your edit.");
    }
    private static void Save<T>(BillingSession db, T value, T? before) where T : class, IBillingEntity
    { if (before is null) db.Insert(value); else db.Update(value, before.Version); }
}

public static class BillingCalendar
{
    public static DateTimeOffset Next(DateTimeOffset start, BillingCycle cycle, int anchorDay)
    {
        if (anchorDay is < 1 or > 31) throw BillingError.Invalid("Billing anchor day must be 1–31.");
        var candidate = cycle == BillingCycle.Monthly ? start.AddMonths(1) : start.AddYears(1);
        return new DateTimeOffset(candidate.Year, candidate.Month, Math.Min(anchorDay, DateTime.DaysInMonth(candidate.Year, candidate.Month)),
            start.Hour, start.Minute, start.Second, TimeSpan.Zero).AddTicks(start.Ticks % TimeSpan.TicksPerSecond);
    }
}
