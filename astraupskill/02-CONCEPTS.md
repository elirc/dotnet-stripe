# Concepts, part 1: money, tenancy and time

This chapter covers the three ideas you can learn one at a time: what money is, who is allowed to touch it, and which clock decides when a period ends. The three ideas that only make sense together — optimistic concurrency, idempotency and the transaction boundary — are in [02B-CONCEPTS-CONCURRENCY.md](02B-CONCEPTS-CONCURRENCY.md). Read this file first.

CRUD describes four verbs. A useful application must also define who may perform them, what remains true afterward, and what happens when a request is repeated or interrupted. Billing makes these questions visible because deleting or rewriting history can change the meaning of an invoice.

## Money is a value with a currency

The [Money value object](../src/Billing.Api/Domain/Money.cs) stores an integer number of minor units. USD 12.34 is `1234` plus currency `USD`. It supports USD, EUR and GBP, all with two decimal places. An input of 12.345 is rejected rather than silently rounded. Arithmetic between different currencies is rejected; this application has no exchange-rate feature.

```csharp
var baseFee = Money.FromMajor(20m, "USD");
var usageFee = Money.FromMajor(.10m, "USD").Multiply(50);
var subtotal = baseFee.Add(usageFee); // 2,500 minor units
var discount = subtotal.Multiply(.20m); // 500 minor units
var total = subtotal.Subtract(discount); // USD 20.00
```

Percentage multiplication rounds half a minor unit away from zero. That is an explicit policy, not an accidental consequence of using `double`. The per-value cap is 900,000,000,000 minor units. Together with the 5,000-record workspace cap, it keeps aggregate invoice minor-unit totals within JavaScript's exact integer range. These bounds are workshop design choices, not universal accounting standards.

An invoice snapshots its lines and amounts. Renaming a plan later does not rewrite an old invoice. Referenced plan economics and coupon discount terms are immutable; create a replacement offer to change them. Archiving blocks new subscriptions while existing agreements continue under their stored references.

## Identity and authorization are different

A bearer token proves possession of a session credential. It does not permanently prove that its holder is an Operator. Every operation joins the session to the current workspace membership. Changing that membership to Viewer immediately removes write access. Deleting it revokes access through the relationship to sessions.

The server obtains workspace identity from the session, not from a customer form. Every entity lookup includes that workspace. A UUID is not a permission boundary: guessing or copying a valid UUID must still return 404 outside its workspace. Composite foreign keys back up the service-level relationship checks.

Passwords are derived with PBKDF2-SHA256, a random salt and 600,000 iterations. Session tokens are random and only their SHA-256 hashes are stored. The browser keeps its token in memory. There is no invitation email, account recovery, password-change screen, federated identity or production identity provider in this workshop.

## Billing time and real time serve different purposes

The persistent simulator clock determines periods and accepted usage. Wall-clock UTC determines session expiry, receipt expiry and signature freshness. Advancing billing by a month must not expire every login. Monthly periods preserve an anchor day: January 31 becomes February 28 and then March 31, rather than drifting permanently to the 28th. Usage must be inside the current half-open interval `[start, end)` and no later than the simulator clock. Late usage for an already invoiced period is rejected; credit notes and adjustments are future work.

## Worked failure: the currency that was never checked

`Money` is a value object rather than a `decimal` for a reason you can see in four steps. Suppose a subscription's plan prices in USD and someone attaches a EUR coupon:

| # | What happens | With `Money` | With a bare `decimal` |
|---|---|---|---|
| 1 | Plan base fee 20 | `Money(2000, "USD")` | `20.00m` |
| 2 | Coupon says "5 off" | `Money(500, "EUR")` | `5.00m` |
| 3 | `subtotal.Subtract(discount)` | **throws** — different currencies, and this application has no exchange rate feature | `15.00m` |
| 4 | The invoice | never created; the request is rejected with a useful message | says `15.00`, with no record of which currency was subtracted from which |

Step 4 is a real billing incident in miniature: nothing crashed, the number looks plausible, and the error is only discoverable by someone reconciling the books weeks later. The type system refusing step 3 is the whole benefit. The same argument applies to `12.345` being rejected rather than rounded at input: a fractional minor unit is a sign that the caller and the server disagree about what the value *is*, and quietly rounding hides that disagreement instead of surfacing it.

Two tests in `tests/Billing.Tests/MoneyTests.cs` pin this down: `DifferentCurrenciesCannotBeAdded` expects an `InvalidOperationException` from `Money.Zero("USD").Add(Money.Zero("EUR"))`, and `InputCannotSilentlyRoundAwayFractionalCents` expects an `ArgumentException` from `Money.FromMajor(0.001m, "USD")`. Open that file before the next chapter and note the exception *types* — they differ deliberately: a bad input is an argument problem, mixing currencies is an operation that has no meaning.

## Where to go next

Everything above can be learned one idea at a time. [Part 2](02B-CONCEPTS-CONCURRENCY.md) cannot: optimistic concurrency, idempotency and transactions are three answers to three different questions that arrive in the same request, and the most common junior mistake is using one where another was needed. It opens with a table separating *stale intent*, *repeated intent* and *partial intent*. If you are following the two-part route in the [README](README.md), stop here and come back.
