using Billing.Api.Domain;
using Billing.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Billing.Tests;

public sealed class WorkflowTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "billing-tests-" + Guid.NewGuid().ToString("N"));
    private readonly BillingDatabase database;
    private readonly WorkspaceAuth auth = new();
    private readonly CatalogService catalog = new();
    private readonly BillingEngine engine = new();
    private readonly SessionResponse session;
    private Guid W => session.Actor.WorkspaceId;
    public WorkflowTests()
    {
        database = new BillingDatabase(System.IO.Path.Combine(directory, "test.sqlite"));
        session = database.Transaction(s => auth.Register(s, new RegisterRequest("owner@example.test", "workshop-password", "Owner", "Workshop", "workshop")));
    }
    public void Dispose() => Directory.Delete(directory, recursive: true);
    private T Tx<T>(Func<BillingSession, T> action) => database.Transaction(action);
    private Customer Customer(BillingSession s, string email = "customer@example.test", string currency = "USD") =>
        catalog.Customer(s, W, new CustomerInput("Customer", email, currency));
    private Plan Plan(BillingSession s, string currency = "USD") =>
        catalog.Plan(s, W, new PlanInput("hybrid", "Hybrid", BillingCycle.Monthly, PlanPricingModel.Hybrid, 20, 100, .10m, currency));
    private Subscription Subscription(BillingSession s)
    {
        var customer = Customer(s); var plan = Plan(s);
        return catalog.Subscribe(s, W, new SubscriptionInput(customer.Id, plan.Id, null));
    }
    private Invoice Invoice(BillingSession s)
    {
        var sub = Subscription(s);
        engine.RunCycle(s, session.Actor, new CycleInput(sub.CurrentPeriodEnd));
        return Assert.Single(s.All<Invoice>(W));
    }

    [Fact] public void RecordsSurviveNewDatabaseInstance()
    {
        var created = Tx(s => Customer(s));
        var reopened = new BillingDatabase(database.Path);
        Assert.Equal(created, reopened.Transaction(s => s.Get<Customer>(W, created.Id), false));
    }
    [Fact] public void FailureRollsBackAllWrites()
    {
        Assert.Throws<InvalidOperationException>(() => Tx<int>(s => { Customer(s); throw new InvalidOperationException("Injected failure"); }));
        Assert.Empty(Tx(s => s.All<Customer>(W)));
    }
    [Fact] public void UnknownDatabaseIsRefusedWithoutAdoption()
    {
        var path = System.IO.Path.Combine(directory, "foreign.sqlite"); File.WriteAllBytes(path, []);
        Assert.Throws<InvalidDataException>(() => new BillingDatabase(path));
        Assert.Equal(0, new FileInfo(path).Length);
    }
    [Fact] public void StaleEditPreservesWinner()
    {
        var customer = Tx(s => Customer(s));
        Tx(s => catalog.Customer(s, W, new CustomerInput("Winner", customer.Email, "USD"), customer.Id, 1));
        Assert.Throws<BillingError>(() => Tx(s => catalog.Customer(s, W, new CustomerInput("Loser", customer.Email, "USD"), customer.Id, 1)));
        Assert.Equal("Winner", Tx(s => s.Get<Customer>(W, customer.Id)).Name);
    }
    [Fact] public void DeleteUnusedAndRejectReferencedCustomer()
    {
        var unused = Tx(s => Customer(s, "unused@example.test"));
        Tx(s => catalog.Delete(s, W, "customers", unused.Id, 1));
        var sub = Tx(Subscription);
        Assert.Throws<SqliteException>(() => Tx(s => catalog.Delete(s, W, "customers", sub.CustomerId, 1)));
        Assert.NotNull(Tx(s => s.Get<Customer>(W, sub.CustomerId)));
    }
    [Fact] public void TenantReadsAndForeignKeysRejectCrossWorkspaceReferences()
    {
        var first = Tx(Subscription);
        var other = Tx(s => auth.Register(s, new RegisterRequest("other@example.test", "workshop-password", "Other", "Other", "other")));
        Assert.Throws<BillingError>(() => Tx(s => s.Get<Customer>(other.Actor.WorkspaceId, first.CustomerId)));
        Assert.Throws<BillingError>(() => Tx(s => catalog.Subscribe(s, other.Actor.WorkspaceId, new SubscriptionInput(first.CustomerId, first.PlanId, null))));
        var own = Tx(s => catalog.Customer(s, other.Actor.WorkspaceId, new CustomerInput("Own", "own@example.test", "USD")));
        Assert.Throws<SqliteException>(() => Tx(s => { s.Link<Customer, Plan>(other.Actor.WorkspaceId, own.Id, first.PlanId); return 0; }));
    }
    [Fact] public void EmailUniquenessIsCaseInsensitiveAndTransactional()
    {
        Tx(s => Customer(s));
        Assert.Throws<SqliteException>(() => Tx(s => Customer(s, "CUSTOMER@example.test")));
        Assert.Single(Tx(s => s.All<Customer>(W)));
    }
    [Fact] public void ReferencedPlanAllowsRenameButNotRepricing()
    {
        Tx(Subscription); var plan = Assert.Single(Tx(s => s.All<Plan>(W)));
        var edit = new PlanInput(plan.Code, "Renamed", plan.Cycle, plan.PricingModel, 20, 100, .10m, "USD", false);
        var renamed = Tx(s => catalog.Plan(s, W, edit, plan.Id, 1));
        Assert.False(renamed.IsActive);
        Assert.Throws<BillingError>(() => Tx(s => catalog.Plan(s, W, edit with { BasePrice = 99 }, plan.Id, 2)));
    }
    [Fact] public void CurrencyMismatchRejectsSubscription()
    {
        var customer = Tx(s => Customer(s)); var plan = Tx(s => Plan(s, "EUR"));
        Assert.Throws<BillingError>(() => Tx(s => catalog.Subscribe(s, W, new SubscriptionInput(customer.Id, plan.Id, null))));
        Assert.Empty(Tx(s => s.All<Subscription>(W)));
    }
    [Theory] [InlineData(0, null)] [InlineData(101, null)] [InlineData(10, 5)] [InlineData(null, null)] [InlineData(null, -5)]
    public void InvalidCouponTermsAreRejected(int? percent, int? amount) =>
        Assert.Throws<BillingError>(() => Tx(s => catalog.Coupon(s, W, new CouponInput("bad", "Bad", (decimal?)percent, (decimal?)amount, "USD", null))));
    [Fact] public void FlatPlanRejectsMeteredFields() => Assert.Throws<BillingError>(() => Tx(s => catalog.Plan(s, W,
        new PlanInput("bad", "Bad", BillingCycle.Monthly, PlanPricingModel.Flat, 20, 10, 1, "USD"))));
    [Fact] public void UsageCannotBeFutureOrWrongMetric()
    {
        var sub = Tx(Subscription);
        Assert.Throws<BillingError>(() => Tx(s => catalog.Usage(s, W, new UsageInput(sub.Id, 10, "other", sub.CurrentPeriodStart))));
        Assert.Throws<BillingError>(() => Tx(s => catalog.Usage(s, W, new UsageInput(sub.Id, 10, "api_calls", sub.CurrentPeriodStart.AddSeconds(1)))));
        Assert.Empty(Tx(s => s.All<UsageEvent>(W)));
    }
    [Fact] public void UsageSupportsEditAndDeleteBeforeInvoice()
    {
        var sub = Tx(Subscription);
        var usage = Tx(s => catalog.Usage(s, W, new UsageInput(sub.Id, 10, "api_calls", sub.CurrentPeriodStart)));
        var edited = Tx(s => catalog.Usage(s, W, new UsageInput(sub.Id, 25, "api_calls", sub.CurrentPeriodStart), usage.Id, 1));
        Assert.Equal(25, edited.Quantity);
        Tx(s => catalog.Delete(s, W, "usage-events", usage.Id, 2)); Assert.Empty(Tx(s => s.All<UsageEvent>(W)));
    }
    [Fact] public void BillingCalculatesUsageAndLocksItIntoInvoice()
    {
        var sub = Tx(Subscription);
        var usage = Tx(s => catalog.Usage(s, W, new UsageInput(sub.Id, 150, "api_calls", sub.CurrentPeriodStart)));
        Tx(s => engine.RunCycle(s, session.Actor, new CycleInput(sub.CurrentPeriodEnd)));
        var invoice = Assert.Single(Tx(s => s.All<Invoice>(W)));
        Assert.Equal(2500, invoice.Total.MinorUnits);
        Assert.Equal(invoice.Id, Tx(s => s.Get<UsageEvent>(W, usage.Id)).InvoiceId);
        Assert.Throws<BillingError>(() => Tx(s => catalog.Delete(s, W, "usage-events", usage.Id, 2)));
        Tx(s => engine.RunCycle(s, session.Actor, new CycleInput(sub.CurrentPeriodEnd)));
        Assert.Single(Tx(s => s.All<Invoice>(W)));
    }
    [Fact] public void CatchupCreatesEveryDuePeriodAndCancelStopsAfterFinalPeriod()
    {
        var sub = Tx(Subscription);
        var third = BillingCalendar.Next(BillingCalendar.Next(sub.CurrentPeriodEnd, BillingCycle.Monthly, sub.AnchorDay), BillingCycle.Monthly, sub.AnchorDay);
        Tx(s => engine.RunCycle(s, session.Actor, new CycleInput(third)));
        Assert.Equal(3, Tx(s => s.All<Invoice>(W)).Count);
        var updated = Tx(s => s.Get<Subscription>(W, sub.Id));
        Tx(s => catalog.EditSubscription(s, W, sub.Id, new SubscriptionEdit(true), updated.Version));
        Tx(s => engine.RunCycle(s, session.Actor, new CycleInput(updated.CurrentPeriodEnd.AddMonths(2))));
        Assert.Equal(4, Tx(s => s.All<Invoice>(W)).Count);
        Assert.Equal(SubscriptionStatus.Canceled, Tx(s => s.Get<Subscription>(W, sub.Id)).Status);
    }
    [Fact] public void InvoiceUniqueIndexPreventsDuplicatePeriod()
    {
        var invoice = Tx(Invoice);
        Assert.Throws<SqliteException>(() => Tx(s => { s.Insert(invoice with { Id = Guid.NewGuid() }); return 0; }));
        Assert.Single(Tx(s => s.All<Invoice>(W)));
    }
    [Fact] public void PaymentUpdatesOneAttemptAndPaidCannotReopen()
    {
        var invoice = Tx(Invoice); var payment = Tx(s => engine.QueuePayment(s, W, invoice.Id, 1));
        Assert.Throws<BillingError>(() => Tx(s => engine.QueuePayment(s, W, invoice.Id, 2)));
        Tx(s => engine.CompletePayment(s, W, invoice.Id, payment.Id, true, "sim-reference"));
        Tx(s => engine.CompletePayment(s, W, invoice.Id, payment.Id, true, "sim-reference"));
        Assert.Throws<BillingError>(() => Tx(s => engine.CompletePayment(s, W, invoice.Id, payment.Id, false, "late")));
        Assert.Equal(InvoiceStatus.Paid, Tx(s => s.Get<Invoice>(W, invoice.Id)).Status);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(Tx(s => s.All<Payment>(W))).Status);
        Assert.Empty(Tx(s => s.All<PaymentJob>(W)));
    }
    [Fact] public void DeclinedAttemptCanRetryWithNewIdentity()
    {
        var invoice = Tx(Invoice); var first = Tx(s => engine.QueuePayment(s, W, invoice.Id, 1));
        Tx(s => engine.Process(s, W, new ProcessInput(false)));
        var second = Tx(s => engine.QueuePayment(s, W, invoice.Id, 3)); Assert.NotEqual(first.Id, second.Id);
        Tx(s => engine.Process(s, W, new ProcessInput(true)));
        Assert.Equal(2, Tx(s => s.All<Payment>(W)).Count);
        Assert.All(Tx(s => s.All<WebhookEvent>(W)), item => Assert.Equal(WebhookDeliveryStatus.Simulated, item.Status));
    }
    [Fact] public void PendingPaymentPreventsVoid()
    {
        var invoice = Tx(Invoice); Tx(s => engine.QueuePayment(s, W, invoice.Id, 1));
        Assert.Throws<BillingError>(() => Tx(s => engine.Void(s, W, invoice.Id, 2)));
        Tx(s => engine.Process(s, W, new ProcessInput(false)));
        Assert.Equal(InvoiceStatus.Void, Tx(s => engine.Void(s, W, invoice.Id, 3)).Status);
        Assert.Throws<BillingError>(() => Tx(s => engine.QueuePayment(s, W, invoice.Id, 4)));
    }
    [Fact] public void DurableReceiptReplaysExactlyAndRejectsChangedRequest()
    {
        var store = new BillingStore(database, auth); var mutation = new Mutation(session.Token, "request-1234", "fingerprint", null);
        var first = store.Write(mutation, "customer.created", (s, _) => Customer(s), 201);
        var restarted = new BillingStore(new BillingDatabase(database.Path), auth);
        Assert.Equal(first, restarted.Write(mutation, "customer.created", (_, _) => throw new Exception("Must not run"), 201));
        Assert.Throws<BillingError>(() => store.Write(mutation with { Fingerprint = "changed" }, "customer.created", (s, _) => Customer(s), 201));
        Assert.Single(Tx(s => s.All<Customer>(W)));
    }
    [Fact] public async Task ConcurrentSameKeyProducesOneMutation()
    {
        var store = new BillingStore(database, auth); var mutation = new Mutation(session.Token, "concurrent-key", "same", null);
        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() => store.Write(mutation, "customer.created", (s, _) => Customer(s), 201)));
        var results = await Task.WhenAll(tasks); Assert.All(results, item => Assert.Equal(results[0], item));
        Assert.Single(Tx(s => s.All<Customer>(W)));
    }
    [Fact] public void ReceiptAndAuditRollBackWithMutationFailure()
    {
        var store = new BillingStore(database, auth); var mutation = new Mutation(session.Token, "failed-request", "same", null);
        Assert.Throws<InvalidOperationException>(() => store.Write(mutation, "fail", (s, _) => { Customer(s); throw new InvalidOperationException(); }));
        Assert.Empty(Tx(s => s.All<Customer>(W)));
        Assert.Equal(0, Tx(s => Convert.ToInt64(s.Scalar("SELECT count(*) FROM receipts"))));
        Assert.Equal(1, Tx(s => Convert.ToInt64(s.Scalar("SELECT count(*) FROM audit"))));
    }
    [Fact] public void RoleChangesAndDeletionApplyToExistingSessions()
    {
        var member = Tx(s => auth.AddMember(s, session.Actor, new MemberRequest("member@example.test", "workshop-password", "Member", "Operator")));
        var memberId = JsonSerializer.SerializeToElement(member).GetProperty("id").GetGuid();
        var login = Tx(s => auth.Login(s, new LoginRequest("member@example.test", "workshop-password", "workshop")));
        Tx(s => auth.Require(s, login.Token, write: true));
        Tx(s => auth.ChangeMember(s, session.Actor, memberId, 1, "Viewer"));
        Assert.Equal(403, Assert.Throws<BillingError>(() => Tx(s => auth.Require(s, login.Token, write: true))).StatusCode);
        Tx(s => auth.ChangeMember(s, session.Actor, memberId, 2, null));
        Assert.Equal(401, Assert.Throws<BillingError>(() => Tx(s => auth.Require(s, login.Token))).StatusCode);
    }
    [Fact] public void ExpiredSessionCannotReplaySuccessfulReceipt()
    {
        var store = new BillingStore(database, auth); var mutation = new Mutation(session.Token, "expires-key", "same", null);
        store.Write(mutation, "create", (s, _) => Customer(s));
        Tx(s => s.Execute("UPDATE sessions SET expires_at=0"));
        Assert.Equal(401, Assert.Throws<BillingError>(() => store.Write(mutation, "create", (s, _) => Customer(s))).StatusCode);
    }
    [Theory] [InlineData(2025, 1, 31, 2, 28)] [InlineData(2024, 1, 31, 2, 29)] [InlineData(2025, 2, 28, 3, 31)]
    public void MonthlyCalendarPreservesAnchor(int year, int month, int day, int expectedMonth, int expectedDay)
    {
        var next = BillingCalendar.Next(new DateTimeOffset(year, month, day, 12, 30, 0, TimeSpan.Zero), BillingCycle.Monthly, 31);
        Assert.Equal(expectedMonth, next.Month); Assert.Equal(expectedDay, next.Day); Assert.Equal(12, next.Hour);
    }
    [Theory] [InlineData(-301)] [InlineData(301)] [InlineData(999999)]
    public void SignedWebhookRejectsPastAndFuture(int seconds)
    {
        var now = DateTimeOffset.UtcNow; var timestamp = now.AddSeconds(seconds).ToUnixTimeSeconds().ToString();
        var secret = new string('A', 64); var signature = WebhookSignatureVerifier.Sign(secret, timestamp, "{}");
        Assert.Throws<BillingError>(() => WebhookSignatureVerifier.Verify(secret, timestamp, signature, "{}", now));
    }
    [Fact] public void SignatureBindsExactBody()
    {
        var now = DateTimeOffset.UtcNow; var timestamp = now.ToUnixTimeSeconds().ToString(); var secret = new string('A', 64);
        var signature = WebhookSignatureVerifier.Sign(secret, timestamp, "{}");
        WebhookSignatureVerifier.Verify(secret, timestamp, signature, "{}", now);
        Assert.Throws<BillingError>(() => WebhookSignatureVerifier.Verify(secret, timestamp, signature, "{ }", now));
    }
}
