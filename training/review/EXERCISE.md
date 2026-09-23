# Blind Code Review — SubscriptionBillingDashboard (.netstripe)

**Time box: 60 minutes.**

You are the new senior engineer on this team. This is a subscription billing API
(`.NET 8` minimal APIs, in-memory store) that generates invoices, queues payments, and
delivers webhooks — real money math, real idempotency requirements. Review it as if it
is about to go live and a payment-ops person is depending on your sign-off.

## Scope

Read these files (all of them — this is the entire backend, ~1,480 lines):

- `src/Billing.Api/Program.cs`
- `src/Billing.Api/Domain/Money.cs`
- `src/Billing.Api/Domain/BillingModels.cs` (esp. `Coupon.CalculateDiscount`)
- `src/Billing.Api/Domain/BillingEnums.cs`, `BillingClock.cs`
- `src/Billing.Api/Infrastructure/BillingEngine.cs`
- `src/Billing.Api/Infrastructure/BillingStore.cs`
- `src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs`
- `src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs`
- `src/Billing.Api/Infrastructure/PaymentWorker.cs`
- `src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs`
- `src/Billing.Api/Infrastructure/BillingOptions.cs`
- `src/Billing.Api/Contracts/Requests.cs`, `Responses.cs`

## Task

1. List every finding you can support with a file + line reference. For each:
   - **Severity**: blocker / high / medium / low
   - **Category**: correctness, security, data integrity, concurrency, failure handling,
     performance, operability, tests, maintainability
   - **Evidence**: file:line or a short quote
   - **Why it matters in production**: what breaks, for whom, and how you'd find out
     (alert, support ticket, audit) before you had a name for it
2. Flag which findings look like **old-AI-generated smells**: fake/simulated integrations
   presented as real, tests-without-assertions (there are no tests — is that itself the
   smell, or is it something else?), hardcoded secrets/fallback values, missing
   transaction boundaries, docs claiming things the code doesn't do.
3. Rank your list by production severity, worst first.
4. Don't fix anything yet. This is a review, not a PR.

## Constraints

- No running the app is required to find most of this — it's a static read.
- Try `dotnet build src/Billing.Api` before anything else. Don't assume a repo with this
  much curriculum written about it is in a working state just because it looks
  well-organized.
- Cite the actual field/method names as they appear in the file, not paraphrases.

## When you're done

Compare against `training/_answers/review.md` using `training/_answers/COMPARE.md`.
Do not open the answer file first — you will not be able to un-see it.
