# Technical questions — SubscriptionBillingDashboard (.netstripe)

All 10 reference this codebase specifically. Answer out loud, citing files/lines, before
checking your work against the code.

1. **API design.** `POST /api/invoices/{invoiceId}/pay` returns `202 Accepted`
   (`Program.cs:211-221`), not `200 OK` or `201 Created`. Why is `202` the right status
   code here specifically, given what `BillingEngine.QueuePayment` actually does
   (`BillingEngine.cs:129-155`)? What would change if it returned `200` instead?

2. **API design / idempotency.** `IdempotencyMiddleware.RequiresIdempotency`
   (`IdempotencyMiddleware.cs:70-72`) applies to every `POST/PUT/PATCH/DELETE` under
   `/api`. Is that the right scope, or too broad/too narrow? Specifically: should
   `POST /api/webhooks/payment-provider` be in scope, given it never receives an
   `Idempotency-Key` header from a real payment provider? What's the actual protection
   gap there (see `training/_answers/review.md` finding 2)?

3. **Data modeling.** `BillingStore` (`BillingStore.cs`) is an in-memory `Dictionary`-backed
   singleton with hand-rolled locking. If you had to move this to PostgreSQL tomorrow,
   which of the `lock (_gate)` blocks would become database transactions, and which would
   need something stronger than a single-row transaction (hint: `RunBillingCycle` /
   `GenerateInvoice`'s check-then-write on `CurrentPeriodEnd`, finding 1)?

4. **Data modeling / money.** Why is `Money` a `readonly record struct` (`Domain/Money.cs:5`)
   rather than a `class`? What would break (or silently change behavior) if it were a
   `class` instead, given how it's used inside `record with` expressions throughout
   `BillingModels.cs`?

5. **Concurrency.** Walk through exactly how two concurrent calls to
   `POST /api/simulator/run-cycle` for the same `billingDate` could generate two invoices
   for the same subscription and period. Which specific method calls interleave, and
   which lock (if any) would need to span the whole sequence to prevent it?

6. **Testing.** There are zero test files in this repository. If you could write exactly
   three tests before touching anything else, which three, and why those three
   specifically (not "test everything" — defend the prioritization)? Cross-reference
   `training/review/characterization-tests-billingengine.md` if you did that exercise.

7. **Security.** `WebhookSignatureVerifier.IsValid` (`WebhookSignatureVerifier.cs:11-40`)
   checks a timestamp tolerance and uses `CryptographicOperations.FixedTimeEquals` for the
   signature comparison. Why constant-time comparison specifically, here? What attack does
   the 5-minute timestamp tolerance defend against, and what happens to a legitimately
   slow/retried webhook delivery that arrives 6 minutes late?

8. **Security.** There is no authentication anywhere in `Program.cs`. If you had to add
   the minimum viable auth to make this safe to deploy (not gold-plated — minimum
   viable), what would you add first, and why is it more urgent than, say, adding rate
   limiting?

9. **Operations/reliability.** `PaymentWorker` and `WebhookDeliveryWorker`
   (`Infrastructure/PaymentWorker.cs`, `WebhookDeliveryWorker.cs`) are both
   `BackgroundService`s inside the same process as the HTTP API. What happens to queued
   payment jobs and webhook events on a normal `dotnet` restart/redeploy? What's the
   minimum change to make that safe?

10. **Domain correctness.** `BillingEngine.GenerateInvoice`'s metered-usage line
    (`BillingEngine.cs:46-60`) sums `UsageEvent.Quantity` across *all* `Metric` values for
    a subscription against one `IncludedUnits`/`UnitPrice` pair. Design (verbally,
    no code) how you'd change the data model and the invoicing logic to correctly support
    a plan with two independently-priced metered metrics (e.g. `api_calls` and
    `storage_gb`). What tables/fields change, and what's the migration risk for existing
    `UsageEvent` rows?
