# On-ramp: 8 warm-ups on .netstripe

Do these before the `training/` material. File references are to `main` @ `a84f11b` (the repo's snapshot commit).
Exercises 2–7 need only reading, and exercise 8 needs the test project from Session 3. Write your answer before you
open `learn/_answers/00-onramp.md`. Time: about 3 hours.

---

## 1. Read two compiler errors (15 min)
`dotnet build src/Billing.Api` on `main` fails with CS0246 and CS0173 (see `learn/README.md` §Known state).
1. CS0246 says a type "could not be found". Which type is it, in which file and at which line, and in which namespace is
   that type declared? (Grep for its declaration.)
2. For CS0173, look at `Program.cs:143`. Which *two* types can't be unified? Why does `Money?` fix it, and why would
   `Money? amountOff = request.AmountOff is null ? null : Money.FromMajor(...)` *also* fix it?
3. The `docs2/` tree has 47 files describing this code. What does the build failure tell you about how much to trust
   those files' claims about runtime behavior?

## 2. Where does each parameter come from? (15 min)
Minimal APIs bind lambda parameters from different sources. For each parameter below, say whether it comes from the
route, the JSON body, DI or the raw request, and name the DI lifetime where it applies (`Program.cs:14-20`).
- `Program.cs:67`: `CreateCustomerRequest request`, `BillingStore store`, `IBillingClock clock`
- `Program.cs:211`: `Guid invoiceId`, `PayInvoiceRequest request`, `BillingEngine engine`
- `Program.cs:223`: `HttpRequest request`, `WebhookSignatureVerifier verifier`

Then: why does the webhook endpoint read the raw body (lines 225-228) instead of taking a typed record parameter?

## 3. Predict `Money` (15 min)
Read `Domain/Money.cs`. What does each line produce? Give `MinorUnits` and `Currency`, or name the exception.
```csharp
a) Money.FromMajor(29m, "usd")
b) Money.FromMajor(0.005m, "USD")
c) Money.FromMajor(-0.005m, "USD")
d) new Money(9900, "USD").Multiply(1m / 12m)
e) new Money(100, "USD").Add(new Money(100, "EUR"))
f) Money.FromMajor(10m, "USD") == new Money(1000, " usd ")
g) new Money(0, "")
```
Then explain why `Money` stores a `long` of minor units instead of a `decimal` amount.

## 4. Invoice the seeded `scale` subscription by hand (25 min)
Seed data: `Infrastructure/BillingStore.cs:267-321`. The subscription is `062a6a8f-...` (Acme, plan `scale`, coupon
`LAUNCH20`). Assume `BillingEngine.GenerateInvoice(subA.Id, billingDate)` runs with `billingDate = now + 1 day`.
Follow `BillingEngine.cs:9-113` and compute:
1. total usage units, and the billable units after `IncludedUnits`;
2. each `InvoiceLine` (description, quantity, amount);
3. `Subtotal`, `Discount` and `Total`, in minor units;
4. the subscription's new `CurrentPeriodStart` and `CurrentPeriodEnd`, relative to `now`.

## 5. Follow a payment (20 min)
Someone calls `POST /api/invoices/{id}/pay` for an Open invoice. Trace it through `Program.cs:211-221`, then
`BillingEngine.QueuePayment`, then `BillingStore.EnqueuePaymentJob`, then `PaymentWorker.ExecuteAsync` / `Process`, then
`BillingEngine.ApplyPaymentResult`, then `WebhookDeliveryWorker`.
1. What HTTP status does the call return, and what's in the body?
2. List every record that gets written (`Payment`, `Invoice` and `WebhookEvent` rows), in order, and how many seconds
   after the call each write happens with default options (`Infrastructure/BillingOptions.cs`).
3. How many `Payment` rows exist for this invoice at the end, and with which statuses?
4. Read `PaymentWorker.cs:40-44`. Where does "succeeded" come from? What would you *expect* it to depend on in a real system?

## 6. Idempotency middleware sequence (20 min)
Read `Infrastructure/IdempotencyMiddleware.cs:9-68`. The same client sends this sequence against one Open invoice. For
each request, predict the status code, and say whether the handler actually ran or the response was replayed.
| # | Request |
|---|---|
| 1 | `POST /api/invoices/{id}/pay`, `Idempotency-Key: key-0001`, body `{"provider":"card"}` |
| 2 | the same as #1, again |
| 3 | the same key, body `{"provider":"ach"}` |
| 4 | `Idempotency-Key: k1` (2 characters) |
| 5 | no `Idempotency-Key` header, same body as #1 |
| 6 | `GET /api/invoices` with `Idempotency-Key: key-0001` |

Bonus: two copies of request #1 with a *new* key arrive at the same moment. Does the handler run once or twice? Point
at the lines that decide it.

## 7. Webhook signature check (15 min)
Read `Infrastructure/WebhookSignatureVerifier.cs`. For each request, say whether `IsValid` returns true or false, and why:
- a) a correct HMAC, with a timestamp 2 minutes old;
- b) a correct HMAC, with a timestamp 10 minutes old;
- c) a correct HMAC, with a timestamp **10 minutes in the future**;
- d) a correct HMAC, but the signature is sent in UPPERCASE hex;
- e) the right timestamp, but the body was re-serialized by a proxy (whitespace changed).

Which of these is a latent bug? Also, why does line 25 use `DateTimeOffset.UtcNow` when the rest of the app injects
`IBillingClock`, and why does that matter for testing?

## 8. Your first money test (30 min, needs the test project from Session 3)
Write xUnit tests that pin `Money.FromMajor` and `Money.Multiply`. Requirements:
- At least one input whose exact result lands exactly on .5 of a minor unit, one above .5 and one below .5.
- At least one negative amount.
- The expected values are integer literals that you computed by hand.

Then do the Session 3 mutation check. At `Money.cs:14` and `Money.cs:45`, change `MidpointRounding.AwayFromZero` to
`MidpointRounding.ToEven` (banker's rounding) in a throwaway edit. Which of your tests fail? If none fail, explain what
your inputs had in common. Revert the edit afterwards.
