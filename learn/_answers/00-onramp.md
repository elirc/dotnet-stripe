# SEALED — attempt first. Answers to learn/00-onramp.md (.netstripe)

Lines refer to `main` @ `a84f11b`. This was a docs-only pass: nothing here was executed. The arithmetic was done by hand
from the source, and runtime claims are marked UNVERIFIED.

## 1. Compiler errors
1. **CS0246.** The missing type is `IBillingClock`. It's used by `IdempotencyStore(IBillingClock clock)` at
   `Infrastructure/IdempotencyMiddleware.cs:90` and declared in namespace `Billing.Api.Domain`
   (`Domain/BillingClock.cs:1-6`). The file only imports `System.Security.Cryptography`, `System.Text` and
   `Microsoft.Extensions.Options`. Implicit usings cover framework namespaces, never your own project's. The fix is to
   add `using Billing.Api.Domain;`.
2. **CS0173.** The two types are `<null>` and `Money`. `Money` is a non-nullable struct, so there's no conversion
   between the two branches, and `var` provides no target type. `(Money?)null` gives the conditional a natural type of
   `Money?`, because `Money` converts implicitly to `Money?`. An explicit `Money? amountOff = ...` also works, because
   C# 9 target-typed conditionals use the declared type. The constructor of `Coupon` (`BillingModels.cs:27`) takes
   `Money? AmountOff`, so either fix fits.
3. **Trusting the docs.** Every claim in `docs/` and `docs2/` about runtime behavior is unobserved, because the code
   could never have run. Use those docs as a map of *intent*, and verify each behavior yourself. The repo's own
   `README.md:66` admits it wasn't compiled.

## 2. Parameter binding
| Endpoint | Parameter | Source | DI lifetime |
|---|---|---|---|
| `:67` | `CreateCustomerRequest request` | JSON body (a complex type on POST) | |
| `:67` | `BillingStore store` | DI | Singleton (line 15) |
| `:67` | `IBillingClock clock` | DI | Singleton (line 14) |
| `:211` | `Guid invoiceId` | route (`{invoiceId:guid}`) | |
| `:211` | `PayInvoiceRequest request` | JSON body | |
| `:211` | `BillingEngine engine` | DI | Singleton (line 16) |
| `:223` | `HttpRequest request` | special type, bound from `HttpContext` | |
| `:223` | `WebhookSignatureVerifier verifier` | DI | Singleton (line 18) |

The webhook needs the **exact bytes** that were signed. The HMAC is computed over `"{timestamp}.{rawBody}"`
(`WebhookSignatureVerifier.cs:30`). Deserializing and re-serializing would change whitespace and property order, and
then no valid signature would verify. The endpoint verifies first and deserializes second (lines 230-235).

## 3. Money
| # | Result |
|---|---|
| a | 2900, "USD" (the currency is trimmed and upper-cased) |
| b | 1. 0.5 of a minor unit, rounded AwayFromZero, becomes 1. |
| c | -1. AwayFromZero is symmetric. |
| d | **825**. `1m/12m` is `0.0833333333333333333333333333` (decimal's 28-digit limit), so the product is 824.99999…, *not exactly* 825. `decimal.Round` then brings it back to 825. Remember that this intermediate isn't exact. |
| e | `InvalidOperationException("Currency mismatch: USD vs EUR.")` (line 65) |
| f | **true.** Record-struct equality compares `MinorUnits` and `Currency`, and `" usd "` normalizes to `"USD"`. |
| g | `ArgumentException("Currency is required.")` |

Why a `long` of minor units: integer arithmetic is exact, sums never grow sub-cent tails, and every place that
*creates* fractions (`FromMajor`, `Multiply`) is forced to make an explicit rounding decision. A `decimal` would also be
exact in base 10, but it lets fractional cents flow silently through totals. A `double` would be wrong outright.

## 4. Invoicing the `scale` subscription
- **Usage:** 8 events, each `18_000 + i × 2_500` for i = 0…7 (`BillingStore.cs:316-320`). The total is
  8 × 18,000 + 2,500 × (0 + 1 + … + 7) = 144,000 + 70,000 = **214,000**. All of them fall between `now-20d` and `now-6d`,
  which is inside the period `*now-30d, now+1d)`.
- **Billable units:** 214,000 − 100,000 = **114,000**.
- **Lines** (the plan is `Hybrid`, so it gets both lines):
  1. `"Scale Usage monthly subscription"`, quantity 1, amount **24,900** (that's $249.00).
  2. `"114,000 billable api_calls units"`, quantity 114,000. The unit price is `FromMajor(0.01)` = 1 minor unit, so the
     amount is `1 × 114,000` = **114,000** ($1,140.00). The `N0` formatting depends on culture, so an invariant culture
     shows "114,000".
- **Subtotal** = **138,900**. The coupon `LAUNCH20` expires at `now+3mo`, after the billing date, so it applies:
  `138,900 × 0.20` = 27,780 exactly, so the **Discount** is **27,780**. There's no `AmountOff`, and 27,780 ≤ 138,900,
  so the discount isn't capped. **Total** = **111,120** ($1,111.20).
- **Period:** the new start is the old end, `now+1d`, and the new end is `now+1d+1 month` (`BillingEngine.cs:84-88`).
  The usage events get stamped with this `InvoiceId`, and one `invoice.created` webhook is queued.
- These are exactly the literals the Session 5 hybrid-plan characterization test should assert. Note that
  `RunBillingCycle`'s *default* `billingDate` is `clock + 1 month` (`Program.cs:206`), which would also invoice the
  `starter` subscription, whose end is `now+15d`.

## 5. Following a payment
1. **202 Accepted**, with `Location: /api/invoices/{id}`. The body is the new `Payment`: `Pending`,
   `Amount = invoice.Total`, and `Provider` is the request's provider or `"simulated"`.
2. The writes, in order:
   - At t=0: a `Payment` (Pending), and a `PaymentJob` with `AvailableAt = now` (`BillingEngine.cs:152-153`).
   - At the next `PaymentWorker` tick, within 4 s (`PaymentWorkerIntervalSeconds = 4`): `ApplyPaymentResult` writes
     the `Invoice` as Paid with `PaidAt`, a **second** `Payment` (Succeeded, provider `"provider-webhook"`,
     reference `pay_…`), and a `WebhookEvent` `invoice.payment_succeeded` (Pending).
   - At the next `WebhookDeliveryWorker` tick, within 8 s: the webhook is marked `Succeeded`, with
     `LastError = "Simulated delivery..."`. **Nothing is sent** (review finding 4).
3. **Two** `Payment` rows. The original one stays `Pending` forever, because nothing updates it, and a separate one is
   `Succeeded`. That's a data-model smell worth writing up during the review.
4. It comes from the literal `succeeded: true` (`PaymentWorker.cs:42`). In a real system it would depend on the payment
   provider's API response. That's why the webhook path and the simulator contradict each other (review finding 3).

## 6. Idempotency sequence
The line numbers below are file lines in `IdempotencyMiddleware.cs`.
| # | Status | Ran or replayed? |
|---|---|---|
| 1 | 202 | Ran. It's cached because it's 2xx (line 63). |
| 2 | 202 | **Replayed**, with `Idempotency-Replayed: true` (lines 35-48). The handler didn't run. |
| 3 | 409 | Same `cacheKey` (method + path + key, line 33) but a different `requestHash` (line 32). |
| 4 | 400 | The key length is below 8 (line 24). |
| 5 | depends on timing | No key, so there's no protection (lines 17-21) and the handler runs. If the worker has already marked the invoice Paid (4 s or more after #1), it returns 400 "Invoice is already paid." Otherwise it returns 202 and a **second payment is queued**. |
| 6 | 200 | GET isn't covered (`RequiresIdempotency`, line 70). |

**Bonus:** the handler runs **twice**. `TryGet` (line 35) and `Set` (line 66) each take the lock separately, and nothing
reserves the key while the first request is still running (lines 51-55). Both requests miss the cache and both run
`QueuePayment`. The fix is an "in-flight" placeholder that's set atomically on the first miss. See
[02 Idempotency* (external apprenticeship note, not in this repo).

## 7. Webhook signature
| Case | Result | Why |
|---|---|---|
| a) 2 min old | true | |
| b) 10 min old | false | The age exceeds the 5-minute tolerance. |
| c) 10 min in the future | **true** | `UtcNow - timestamp` is negative, so it's never `> 5 min` (`WebhookSignatureVerifier.cs:25`). |
| d) uppercase hex | true | Both sides are lower-cased (lines 34-35). |
| e) re-serialized body | false | The HMAC is over the raw bytes. This is correct behavior. |

- **(c) is the latent bug.** A captured, validly signed request with a far-future timestamp stays replayable for as
  long as that timestamp says. Combined with the missing `EventId` dedup (review finding 2, and ticket M1), a replay is
  processed again. UNVERIFIED (reasoned from the arithmetic, not run). The review's finding 10 covers the clock half of
  this.
- **The clock:** line 25 uses `DateTimeOffset.UtcNow`, so a test can't freeze time. You can't write a deterministic
  test for "exactly 5 minutes old" without injecting `IBillingClock`.

## 8. Money tests (a reference)
```csharp
[Theory]
[InlineData(0.005, 1)]    // exact midpoint: AwayFromZero -> 1 (ToEven -> 0)
[InlineData(0.025, 3)]    // exact midpoint: AwayFromZero -> 3 (ToEven -> 2)
[InlineData(-0.005, -1)]  // negative midpoint
[InlineData(0.007, 1)]    // above .5
[InlineData(0.003, 0)]    // below .5
public void FromMajor_rounds(decimal major, long expectedMinor) =>
    Assert.Equal(expectedMinor, Money.FromMajor(major, "USD").MinorUnits);

[Theory]
[InlineData(5, 0.5, 3)]      // 2.5 -> 3 (ToEven -> 2)
[InlineData(7, 0.1, 1)]      // 0.7 -> 1
[InlineData(3, 0.1, 0)]      // 0.3 -> 0
public void Multiply_rounds(long minor, decimal factor, long expected) =>
    Assert.Equal(expected, new Money(minor, "USD").Multiply(factor).MinorUnits);
```
- **The ToEven mutation:** the midpoint rows fail (0.005, 0.025, −0.005 and 5 × 0.5). If your inputs were all whole
  dollars, or multiples that divide evenly, nothing fails. A money test suite built only from round numbers can't see
  rounding bugs. Remember this during incident 002.
- In xUnit, `[InlineData]` can't hold `decimal` literals directly, so the `double` values above are converted. Check
  that the conversion is exact for your values, or use `MemberData` with real `decimal`s.
