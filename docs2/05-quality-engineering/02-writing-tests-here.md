> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/05-quality-engineering/02-writing-tests-here.md.txt).

# 05.02 — Writing Tests Here

Concrete recipes for the **first tests this repo has ever had**. All test code is
`// Illustrative: not from this repo` — it's what you'd write, validated against the real API shapes,
not copied from existing tests (there are none). Commands are **inferred** (no .NET SDK on the
authoring host — [verification-log](../08-reference/verification-log.md)); run them after installing
.NET 8.

## Recipe 0 — Bootstrap the test project

```powershell
# inferred
dotnet new xunit -o tests/Billing.Api.Tests
dotnet add tests/Billing.Api.Tests reference src/Billing.Api/Billing.Api.csproj
dotnet add tests/Billing.Api.Tests package Microsoft.AspNetCore.Mvc.Testing   # for WebApplicationFactory
dotnet sln add tests/Billing.Api.Tests   # the .sln currently has only Billing.Api
```

Two glue pieces you'll need:

```csharp
// Illustrative: a fixed clock — the key seam (mirrors IBillingClock)
public sealed class TestClock(DateTimeOffset now) : IBillingClock { public DateTimeOffset UtcNow => now; }
```

```csharp
// Illustrative: add this ONE line at the end of Program.cs so WebApplicationFactory<Program> can see it.
// (Top-level-statements generate an internal Program; this marker makes it referenceable. Additive, harmless.)
public partial class Program;
```

> That `public partial class Program;` is the only product-code touch the harness needs — it's the
> learner's contribution, additive, and changes no behavior.

## Recipe 1 — Happy path: pin the invoice total (the most valuable test in the repo)

```csharp
// Illustrative
[Fact]
public void GenerateInvoice_ScaleSubscription_TotalsToExpected()
{
    var clock  = new TestClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store  = new BillingStore(clock);                 // self-seeds with fixed GUIDs
    var engine = new BillingEngine(store, clock);
    var scaleSubId = Guid.Parse("062a6a8f-9afe-446f-8e7f-dbc4e7a20f2d");   // seeded Scale subscription

    var invoice = engine.GenerateInvoice(scaleSubId, clock.UtcNow.AddDays(35));

    Assert.Equal(111120, invoice.Total.MinorUnits);   // $1,111.20  (Flow 2 arithmetic)
    Assert.Equal("USD",  invoice.Total.Currency);
    Assert.Equal(InvoiceStatus.Open, invoice.Status);
    Assert.Equal(2, invoice.Lines.Count);             // recurring + usage
}
```

This pins the exact number from [Flow 2](../01-codebase-cartography/05-key-flows.md). If a future
change breaks money math, this fails. The fixed clock makes the seed deterministic (see the testing-
strategy caveat).

## Recipe 2 — Validation failure (integration, `WebApplicationFactory`)

```csharp
// Illustrative
public class CustomerApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateCustomer_MissingEmail_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/customers",
            new { name = "Acme", email = "", currency = "USD" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);   // Program.cs L69-L72
    }
}
```

## Recipe 3 — Idempotency replay vs 409

```csharp
// Illustrative
[Fact]
public async Task SameKeySameBody_Replays_DifferentBody_Conflicts()
{
    var client = factory.CreateClient();
    var key = Guid.NewGuid().ToString();
    var body = new { name = "Acme", email = "a@x.example", currency = "USD" };

    var first  = await Post(client, "/api/customers", body, key);   // 201
    var replay = await Post(client, "/api/customers", body, key);   // replay
    var clash  = await Post(client, "/api/customers", new { name = "B", email = "b@x.example", currency = "USD" }, key);

    Assert.Equal(HttpStatusCode.Created, first.StatusCode);
    Assert.True(replay.Headers.Contains("Idempotency-Replayed"));   // IdempotencyMiddleware L46
    Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);        // IdempotencyMiddleware L39-L42
}
```

## Recipe 4 — Cross-tenant / permission failure (the test you CAN'T write yet)

There is no auth and no tenant concept (R-02), so a cross-tenant rejection test **cannot be written
today** — there's nothing to reject against. That's not an omission to paper over; it's the test that
*proves* the gap:

```csharp
// Illustrative — this test SHOULD exist and currently can't, because there is no authorization.
[Fact(Skip = "No auth/tenant isolation yet — see R-02. This test defines the missing behavior.")]
public async Task PayInvoice_ForAnotherTenant_Returns404() { /* ... */ }
```

Writing the skipped test first is a legitimate way to document required-but-missing behavior.

## Recipe 5 — Async side effect (test the engine path, not the timer)

Don't wait on the real 4-second `PeriodicTimer` — that's flake. Test the engine method the worker
calls:

```csharp
// Illustrative
[Fact]
public void ApplyPaymentResult_Success_MarksInvoicePaid_AndRecordsPayment()
{
    var clock  = new TestClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var store  = new BillingStore(clock);
    var engine = new BillingEngine(store, clock);
    var invoice = engine.GenerateInvoice(Guid.Parse("062a6a8f-9afe-446f-8e7f-dbc4e7a20f2d"), clock.UtcNow.AddDays(35));

    engine.ApplyPaymentResult(invoice.Id, succeeded: true, providerReference: "pay_test", failureReason: null);

    Assert.True(store.TryGetInvoice(invoice.Id, out var paid));
    Assert.Equal(InvoiceStatus.Paid, paid.Status);
    Assert.Single(store.Payments().Where(p => p.InvoiceId == invoice.Id && p.Status == PaymentStatus.Succeeded));
}
```

## Recipe 6 — The currency bug, as a failing test (R-01)

```csharp
// Illustrative — RED today; turns GREEN when R-01 is fixed.
[Fact]
public async Task Dashboard_WithEuroPlanActiveSubscription_Returns200()
{
    var client = factory.CreateClient();
    // create EUR plan + customer + subscription via the API, then:
    var resp = await client.GetAsync("/api/dashboard");
    Assert.Equal(HttpStatusCode.OK, resp.StatusCode);   // currently expected to 500 (CalculateMetrics L262)
}
```

A failing test that documents a known bug is a *spec*. Mark it `Skip` with the risk id if you don't
want CI red, but write it.

## Recipe 7 — UI state (React Testing Library, brief)

```tsx
// Illustrative
test("shows 'Load failed' when the dashboard fetch rejects", async () => {
  vi.spyOn(global, "fetch").mockRejectedValue(new Error("boom"));
  render(<App />);                                  // App.tsx L158-L164 renders the error state
  expect(await screen.findByText(/Load failed/)).toBeInTheDocument();
});
```

## Running them

```powershell
# inferred (no .NET SDK on the authoring host)
dotnet test
dotnet test --filter "FullyQualifiedName~GenerateInvoice"
```

## Drill

Implement Recipe 1 for real (in a scratch branch) and run it. Did it pass at `111120`? If not, which
seed assumption broke — the clock, or a usage event outside the window? **Self-grade — Basic:** it
compiles and runs. **Solid:** it passes at the expected total. **Strong:** you also added Recipe 6 and
confirmed it's red, documenting R-01 as an executable spec.

## Verification Notes

All test code is illustrative and validated against the real method signatures and seed GUIDs
([store Seed L267-L321](../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L321),
[engine constructor L7](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7)) read on 2026-06-09.
The `$1,111.20` total is the hand-traced Flow 2 figure, **not** produced by running these tests — no
.NET SDK was available. Run them to confirm; report any divergence.
