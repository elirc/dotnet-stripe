> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/04-code-reading-gym/03-fake-code-contrasts.md.txt).

# 04.03 — Fake-Code Contrasts

Bad-vs-better pairs. Every "bad" version is something a real contributor would plausibly write against
*this* repo; every "better" version ties back to a pattern the repo already uses. All code here is
`// Illustrative: not from this repo` unless it's an anchored quote.

---

## Contrast 1 — Money as a primitive vs a value object

```csharp
// Illustrative: not from this repo — BAD
public decimal CalculateTotal(decimal subtotal, decimal discountPct)
    => subtotal - (subtotal * discountPct / 100);   // float-ish drift across many lines, no currency
```

```csharp
// Better — what this repo actually does
var subtotal = lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.Amount));
var discount = coupon?.CalculateDiscount(subtotal, billingDate) ?? Money.Zero(currency);
var total    = subtotal.Subtract(discount).Max(Money.Zero(currency));
```

**Why the repo's way wins:** integer minor units don't drift, and `Money.Add` *refuses* to mix
currencies. **Real anchor:** [GenerateInvoice L62-L64](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L64),
[Money](../../src/Billing.Api/Domain/Money.cs#L5-L68). **The trap:** the bad version looks simpler and
is wrong at the 3rd decimal.

---

## Contrast 2 — Trusting an id from the body vs scoping to the caller

```csharp
// Illustrative: not from this repo — BAD (and exactly what the repo does today, R-02)
api.MapPost("/usage-events", (CreateUsageEventRequest req, BillingStore store) =>
    store.AddUsageEvent(/* uses req.SubscriptionId directly */));   // any caller, any subscription
```

```csharp
// Better — once auth exists
api.MapPost("/usage-events", (CreateUsageEventRequest req, BillingStore store, ICurrentUser user) =>
{
    if (!store.TryGetSubscription(req.SubscriptionId, out var sub) || sub.TenantId != user.TenantId)
        return Results.NotFound();   // 404, not 403 — don't confirm existence
    return store.AddUsageEvent(/* ... */);
});
```

**Why it matters:** the bad version is an IDOR — supply any `Guid`, act on anyone's subscription.
**Real anchor:** [usage-events endpoint L178-L202](../../src/Billing.Api/Program.cs#L178-L202) (the
repo currently *is* the bad version). **Lesson:** ownership is checked, not assumed.

---

## Contrast 3 — N+1 per-item query vs batch

```csharp
// Illustrative: not from this repo — BAD (in a DB world)
foreach (var sub in subscriptions)
{
    var usage = db.UsageEvents.Where(u => u.SubscriptionId == sub.Id && u.InvoiceId == null).ToList(); // 1 query PER sub
    // ...
}
```

```csharp
// Better — one query, grouped in memory
var usageBySub = db.UsageEvents.Where(u => u.InvoiceId == null && /* in any open period */)
                               .GroupBy(u => u.SubscriptionId)
                               .ToDictionary(g => g.Key, g => g.ToList());   // 1 query
```

**Why it matters:** `RunBillingCycle` loops subscriptions and fetches usage per subscription
([engine L118-L124](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L118-L124) →
[GetUnbilledUsage L182-L188](../../src/Billing.Api/Infrastructure/BillingStore.cs#L182-L188)). In
memory it's a scan-per-sub (slow but fine); as EF it's a classic **N+1**. **Lesson:** the shape that's
"merely slow" in memory becomes a round-trip-per-row against a DB.

---

## Contrast 4 — Swallowing exceptions vs mapping them

```csharp
// Illustrative: not from this repo — BAD
try { return Results.Created(...); }
catch { return Results.Ok(); }   // hides the failure as a success — the worst outcome
```

```csharp
// Better — what this repo actually does
try { return Results.Created($"/api/customers/{customer.Id}", store.AddCustomer(customer)); }
catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
{
    return BadRequest(ex.Message);   // map the *expected* domain error to 400; let unexpected ones bubble
}
```

**Why the repo's way wins:** the `when` filter catches only *expected* domain exceptions and surfaces
them as 400; an *unexpected* exception still bubbles (so you'd see it, not silently 200). **Real
anchor:** [create customer L74-L82](../../src/Billing.Api/Program.cs#L74-L82). **The subtlety:** a bare
`catch` is worse than no catch — it converts failures into fake successes.

---

## Contrast 5 — DTO coupled to entity shape (casual contract change)

```csharp
// Illustrative: not from this repo — BAD: rename a domain field, silently break the client
public sealed record Plan(Guid Id, string Code, string Name, /* renamed */ Money MonthlyPrice, ...);
//                                                            ^ the React api.ts still expects `basePrice`
```

```csharp
// Better — a response DTO decouples the wire from the entity
public sealed record PlanResponse(Guid Id, string Code, string Name, MoneyDto BasePrice, ...)
{ public static PlanResponse From(Plan p) => new(...); }   // rename the entity freely; map at the edge
```

**Why it matters:** today the dashboard serializes `Plan` **raw**
([dashboard L53-L62](../../src/Billing.Api/Program.cs#L53-L62)), and the client mirrors the field names
by hand ([api.ts Plan L24-L35](../../src/Billing.Web/src/api.ts#L24-L35)). Rename `BasePrice` and the
React app breaks with no compiler warning on either side. Only `Invoice` is protected by a DTO
([InvoiceResponse](../../src/Billing.Api/Contracts/Responses.cs#L37-L74)). **Lesson:** raw-entity
responses make every field rename a breaking API change.

---

## Contrast 6 — Side effect in an unreliable place

```csharp
// Illustrative: not from this repo — BAD: fire-and-forget the webhook, no record, no retry
_ = httpClient.PostAsync(url, content);   // if the process dies here, the event is gone forever
```

```csharp
// Better — outbox: persist the intent, deliver async (the repo's shape)
store.EnqueueWebhook(new WebhookEvent(..., WebhookDeliveryStatus.Pending, ...));   // durable intent
// WebhookDeliveryWorker drains and marks delivered/failed later
```

**Why the repo's *shape* wins:** an outbox row survives a crash and can be retried. **Real anchor:**
[EnqueueWebhook in GenerateInvoice L100-L110](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L100-L110),
drained by [WebhookDeliveryWorker](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L26-L54).
**But:** the repo only has the *shape* — delivery is simulated and the retry path is dead (R-06). So
this contrast is "the repo's design is right, its implementation is unfinished" — a nuance worth
saying out loud.

## Drill

Pick Contrast 5 and grep (read) for every other raw-entity response in the dashboard. Which renames
would break the client? **Self-grade — Basic:** you list `Customer`/`Plan`/etc. **Solid:** you note
`Invoice` is the only one safe. **Strong:** you propose a consistent rule (all responses get DTOs, or
none do) and argue which, with the maintenance cost in mind.

## Verification Notes

"Better" versions anchored to real code are read-verified on 2026-06-09. "Bad" versions are labeled
illustrative; where the repo currently *is* the bad version (Contrasts 2, 3-as-future), that's stated
explicitly and tied to a risk-register entry.
