# Exercise — characterization tests for `BillingEngine`

There is no test project anywhere in this repository (`training/_answers/review.md`
finding 17 — confirmed via `find -iname "*test*"`, plus the repo didn't even compile as
committed, finding 0). `BillingEngine`
(`src/Billing.Api/Infrastructure/BillingEngine.cs`) is the single class that touches
money on every invoice, discount, and payment-status transition in the app — it is the
highest-value place to start, and the riskiest place to have zero coverage.

## What a characterization test is (and isn't)

A characterization test doesn't start from a spec — it starts from **what the code
currently does**, on purpose, including its bugs. You write a test, run it, see what
comes out, and assert on that. The point is to pin the current behavior down in an
executable form so that:

- a future refactor that changes behavior gets caught immediately, even if nobody
  remembers why the original behavior was that way;
- you build an accurate map of the class's actual behavior before you touch it, instead
  of trusting the docs (`docs2/03-architecture-and-patterns/02-data-model-and-persistence.md`
  describes `BillingEngine`'s intended behavior — treat it as a hypothesis to verify, not
  a source of truth, since it was written without ever running the code);
- when you *do* find a real bug this way (and you likely will — see below), you get to
  choose whether to assert the buggy behavior with a `// BUG:` comment linking to a
  ticket, or fix it and update the test in the same PR. Don't fix silently.

## Setup

There's no test project yet — creating one is part of this exercise.

```powershell
cd src
dotnet new xunit -n Billing.Api.Tests -o Billing.Api.Tests
dotnet add Billing.Api.Tests reference Billing.Api
cd ..
dotnet sln SubscriptionBillingDashboard.sln add src/Billing.Api.Tests
```

`BillingEngine`'s constructor takes `BillingStore store, IBillingClock clock`. `BillingStore`
seeds itself with fixed data in its constructor (`BillingStore.cs:19-22, 267-321`) driven
by the clock you pass in — use a fake `IBillingClock` that returns a fixed
`DateTimeOffset` so your tests are deterministic and don't depend on `DateTimeOffset.UtcNow`.

## What to cover (write these as separate `[Fact]`/`[Theory]` methods)

1. **`GenerateInvoice` on a flat plan** — call it for the seeded `starter` subscription
   (`86852b42-...`). Assert the resulting `Invoice.Lines` count, the line `Description`,
   `Subtotal`, `Discount`, `Total`, and that `Subscription.CurrentPeriodStart`/`End` moved
   forward by one month after the call (read it back via `store.TryGetSubscription`).
2. **`GenerateInvoice` on a hybrid plan with usage** — the seeded `scale` subscription
   (`062a6a8f-...`) has 8 pre-seeded `UsageEvent`s totaling above `IncludedUnits`
   (100,000). Assert the metered line's `Quantity` and `Amount` come out to what the
   current code actually computes — do the arithmetic by hand first and compare, don't
   just assert "whatever it prints."
3. **`GenerateInvoice` with a coupon** — the `scale` subscription has `CouponId` set to
   `LAUNCH20` (20% off, seeded). Assert the `Discount` line reflects 20% off the subtotal,
   using `Coupon.CalculateDiscount`'s actual current rounding behavior — this is where
   you'll want to double-check `Money.Multiply`'s rounding by hand (see
   `training/_answers/review.md` finding 11, and compare against incident
   `training/incidents/002-mrr-rounding-drift.md` if you've done that one — the bug
   planted there lives in exactly this code path).
4. **`GenerateInvoice` on a canceled subscription** — assert it throws
   `InvalidOperationException`, and check the exact message text (yes, really — this is a
   characterization test; if the message changes later, you want to know).
5. **`GenerateInvoice` on an unknown subscription id** — assert `KeyNotFoundException`.
6. **`RunBillingCycle`** — call it twice in a row with the same `billingDate` for a
   subscription whose `CurrentPeriodEnd <= billingDate`. What actually happens on the
   second call? Write down your prediction *before* running it, then run it and record
   the real result. (This one is deliberately pointed at
   `training/_answers/review.md` finding 1 — write what you find, whether or not it
   matches your prediction, and whether or not it matches what you'd want it to do.)
7. **`QueuePayment` then `ApplyPaymentResult(succeeded: false)`** — assert the invoice
   goes back to `InvoiceStatus.Open`, a `Payment` row with `PaymentStatus.Failed` exists,
   and a `WebhookEvent` with `EventType == "invoice.payment_failed"` was enqueued
   (`store.WebhookEvents()`).
8. **`ApplyPaymentResult` called twice for the same invoice** — once `succeeded: true`,
   then again `succeeded: true`. How many `Payment` rows exist afterward? How many
   `WebhookEvent`s? (This is the shape of the bug in
   `training/_answers/review.md` finding 2 — you're not fixing it here, just pinning down
   and asserting exactly what the current code does, so a later fix has something to
   diff against.)

## Definition of done

- A `Billing.Api.Tests` project exists, is added to the `.sln`, and `dotnet test` passes.
- At least the 8 cases above, each asserting concrete values (not just "doesn't throw"),
  each with a one-line comment saying what real invariant or known gap it's pinning down.
- Where you found behavior that surprised you, write it in a short `NOTES.md` next to the
  test file — not a fix, just what you found and where. That list is the seed for the
  next batch of exercises on this project.

No sealed answer for this one — there isn't a single correct test suite, and writing the
tests *is* the exercise. If you want a sanity check on whether your assertions are
computing the right numbers by hand, the arithmetic paths are fully specified in
`training/_answers/review.md` findings 11 and 12.
