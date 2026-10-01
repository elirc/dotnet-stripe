using System.Net;
using System.Text;
using System.Text.Json;
using Billing.Api.Domain;
using Billing.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Billing.Tests;

public sealed class BillingFactory : WebApplicationFactory<Program>
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "billing-http-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BillingDatabase>();
            services.AddSingleton(new BillingDatabase(Path.Combine(DirectoryPath, "test.sqlite")));
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
    }
}

public sealed class HttpTests : IClassFixture<BillingFactory>
{
    private readonly HttpClient client;
    private readonly BillingFactory factory;
    public HttpTests(BillingFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
    }
    private async Task<SessionResponse> Register()
    {
        var id = Guid.NewGuid().ToString("N");
        var response = await Request("POST", "/api/auth/register", new RegisterRequest(id + "@example.test", "workshop-password", "Owner", "HTTP Lab", "lab-" + id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return BillingJson.Read<SessionResponse>(await response.Content.ReadAsStringAsync());
    }
    private Task<HttpResponseMessage> Request(string method, string path, object? body = null, string? token = null, string? key = null, string? etag = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body is string raw ? raw : JsonSerializer.Serialize(body, BillingJson.Options), Encoding.UTF8, "application/json");
        if (token is not null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return client.SendAsync(request);
    }
    private async Task<Customer> CreateCustomer(SessionResponse session)
    {
        var response = await Request("POST", "/api/customers", new CustomerInput("Ada", "ada@example.test", "USD"), session.Token, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("\"v1\"", response.Headers.ETag?.Tag);
        return BillingJson.Read<Customer>(await response.Content.ReadAsStringAsync());
    }

    [Fact] public async Task RegisterCrudAndEtagRoundtrip()
    {
        var session = await Register(); var customer = await CreateCustomer(session);
        var edit = await Request("PUT", "/api/customers/" + customer.Id, new CustomerInput("Ada edited", customer.Email, "USD"), session.Token, "http-edit-123", "\"v1\"");
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode); Assert.Equal("\"v2\"", edit.Headers.ETag?.Tag);
        var get = await Request("GET", "/api/customers/" + customer.Id, token: session.Token);
        Assert.Equal("Ada edited", BillingJson.Read<Customer>(await get.Content.ReadAsStringAsync()).Name);
        var delete = await Request("DELETE", "/api/customers/" + customer.Id, "{}", session.Token, "http-delete-123", "\"v2\"");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Request("GET", "/api/customers/" + customer.Id, token: session.Token)).StatusCode);
    }
    [Fact] public async Task AnonymousReadsAreDenied() => Assert.Equal(HttpStatusCode.Unauthorized, (await Request("GET", "/api/customers")).StatusCode);
    [Fact] public async Task BodyLimitIsEnforced() => Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
        (await Request("POST", "/api/auth/register", "{\"x\":\"" + new string('x', 33000) + "\"}")).StatusCode);
    [Fact] public async Task ForeignHostAndOriginAreRejected()
    {
        using var host = new HttpRequestMessage(HttpMethod.Get, "/api/health"); host.Headers.Host = "evil.example";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(host)).StatusCode);
        using var origin = new HttpRequestMessage(HttpMethod.Get, "/api/health"); origin.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(origin)).StatusCode);
    }
    [Fact] public async Task UnknownApiRouteDoesNotReturnSpa() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Request("GET", "/api/does-not-exist")).StatusCode);
    [Theory]
    [InlineData("{\"name\":\"A\",\"name\":\"B\",\"email\":\"x@example.test\",\"currency\":\"USD\"}")]
    [InlineData("{\"name\":\"A\",\"email\":\"x@example.test\",\"currency\":\"USD\",\"admin\":true}")]
    [InlineData("{\"name\":null,\"email\":\"x@example.test\",\"currency\":\"USD\"}")]
    [InlineData("{\"email\":\"x@example.test\",\"currency\":\"USD\"}")]
    [InlineData("null")]
    public async Task StrictJsonRejectsAmbiguousBodies(string body)
    {
        var session = await Register();
        Assert.Equal(HttpStatusCode.BadRequest, (await Request("POST", "/api/customers", body, session.Token, Guid.NewGuid().ToString())).StatusCode);
    }
    [Theory] [InlineData(null, 428)] [InlineData("v1", 400)] [InlineData("W/\"v1\"", 400)] [InlineData("\"v99\"", 409)]
    public async Task VersionBoundaryProtectsEdits(string? version, int status)
    {
        var session = await Register(); var customer = await CreateCustomer(session);
        var response = await Request("PUT", "/api/customers/" + customer.Id, new CustomerInput("Changed", customer.Email, "USD"), session.Token, Guid.NewGuid().ToString(), version);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("Ada", factory.Services.GetRequiredService<BillingDatabase>().Transaction(s => s.Get<Customer>(session.Actor.WorkspaceId, customer.Id)).Name);
    }
    [Fact] public async Task ExactHttpRetryReturnsSameBodyAndEtag()
    {
        var session = await Register(); var key = Guid.NewGuid().ToString(); var input = new CustomerInput("Same", "same@example.test", "USD");
        var first = await Request("POST", "/api/customers", input, session.Token, key);
        var second = await Request("POST", "/api/customers", input, session.Token, key);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
        Assert.Equal(HttpStatusCode.Conflict, (await Request("POST", "/api/customers", input with { Name = "Different" }, session.Token, key)).StatusCode);
    }
    [Fact] public async Task PaginationSearchAndBounds()
    {
        var session = await Register(); var db = factory.Services.GetRequiredService<BillingDatabase>();
        db.Transaction(s => { for (var i = 0; i < 25; i++) new CatalogService().Customer(s, session.Actor.WorkspaceId, new CustomerInput("Customer " + i, $"c{i}@example.test", "USD")); return 0; });
        using var page = JsonDocument.Parse(await (await Request("GET", "/api/customers?offset=20&limit=20", token: session.Token)).Content.ReadAsStringAsync());
        Assert.Equal(25, page.RootElement.GetProperty("total").GetInt32()); Assert.Equal(5, page.RootElement.GetProperty("items").GetArrayLength());
        using var search = JsonDocument.Parse(await (await Request("GET", "/api/customers?search=Customer%2024", token: session.Token)).Content.ReadAsStringAsync());
        Assert.Equal(1, search.RootElement.GetProperty("total").GetInt32());
        Assert.Equal((HttpStatusCode)422, (await Request("GET", "/api/customers?limit=1000", token: session.Token)).StatusCode);
    }
    [Fact] public async Task CrossWorkspaceHttpReadIsNotFound()
    {
        var first = await Register(); var second = await Register(); var customer = await CreateCustomer(first);
        Assert.Equal(HttpStatusCode.NotFound, (await Request("GET", "/api/customers/" + customer.Id, token: second.Token)).StatusCode);
    }
    [Fact] public async Task LogoutRevokesToken()
    {
        var session = await Register(); Assert.Equal(HttpStatusCode.OK, (await Request("POST", "/api/auth/logout", token: session.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("GET", "/api/auth/me", token: session.Token)).StatusCode);
    }
    [Fact] public async Task DashboardGroupsCurrencies()
    {
        var session = await Register(); var db = factory.Services.GetRequiredService<BillingDatabase>();
        db.Transaction(s =>
        {
            var catalog = new CatalogService(); var time = CatalogService.BillingTime(s, session.Actor.WorkspaceId);
            foreach (var currency in new[] { "USD", "EUR" })
            {
                var c = catalog.Customer(s, session.Actor.WorkspaceId, new CustomerInput(currency, currency + "@example.test", currency));
                var p = catalog.Plan(s, session.Actor.WorkspaceId, new PlanInput(currency.ToLowerInvariant(), currency, BillingCycle.Monthly, PlanPricingModel.Flat, 10, 0, 0, currency));
                catalog.Subscribe(s, session.Actor.WorkspaceId, new SubscriptionInput(c.Id, p.Id, null));
            }
            return new BillingEngine().RunCycle(s, session.Actor, new CycleInput(time.AddMonths(1)));
        });
        var response = await Request("GET", "/api/dashboard", token: session.Token); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("totals").GetArrayLength());
    }
    [Fact] public async Task SignedCallbackDeduplicatesAndRejectsConflictingEvent()
    {
        var session = await Register(); var db = factory.Services.GetRequiredService<BillingDatabase>(); var engine = new BillingEngine();
        var state = db.Transaction(s =>
        {
            var catalog = new CatalogService(); var w = session.Actor.WorkspaceId;
            var customer = catalog.Customer(s, w, new CustomerInput("Customer", "callback@example.test", "USD"));
            var plan = catalog.Plan(s, w, new PlanInput("callback", "Callback", BillingCycle.Monthly, PlanPricingModel.Flat, 10, 0, 0, "USD"));
            var sub = catalog.Subscribe(s, w, new SubscriptionInput(customer.Id, plan.Id, null));
            engine.RunCycle(s, session.Actor, new CycleInput(sub.CurrentPeriodEnd)); var invoice = Assert.Single(s.All<Invoice>(w));
            var payment = engine.QueuePayment(s, w, invoice.Id, 1);
            var secret = (string)s.Scalar("SELECT secret FROM workspaces WHERE id=$w", ("$w", w.ToString()))!;
            return (invoice, payment, secret);
        });
        async Task<HttpResponseMessage> Callback(bool succeeded)
        {
            var body = BillingJson.Write(new ProviderInput("event-1", state.invoice.Id, state.payment.Id, succeeded, "provider-ref"));
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payment-provider/" + session.Actor.WorkspaceId);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Billing-Timestamp", timestamp); request.Headers.Add("X-Billing-Signature", WebhookSignatureVerifier.Sign(state.secret, timestamp, body));
            return await client.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.OK, (await Callback(true)).StatusCode);
        Assert.Contains("\"duplicate\":true", await (await Callback(true)).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Callback(false)).StatusCode);
        Assert.Equal(InvoiceStatus.Paid, db.Transaction(s => s.Get<Invoice>(session.Actor.WorkspaceId, state.invoice.Id)).Status);
    }
}
