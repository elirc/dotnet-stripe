# Sealed answer — Incident 002

**BUILD: VERIFIED** (`dotnet build src/Billing.Api` succeeds on branch
`training/incidents/002-mrr-rounding-drift`, commit `bfce8de`, after also applying the
two pre-committed build fixes from `training/_answers/review.md` finding 0 — those fixes
are bundled in this commit, unrelated to the planted bug itself.)

## Root cause

`src/Billing.Api/Domain/Money.cs`, `Multiply`, changed from:

```csharp
public Money Multiply(decimal multiplier)
{
    var minor = decimal.Round(MinorUnits * multiplier, 0, MidpointRounding.AwayFromZero);
    return new Money((long)minor, Currency);
}
```

to (the incident diff):

```csharp
public Money Multiply(decimal multiplier)
{
    // decimal.Round + cast was flagged as an unnecessary allocation in profiling;
    // the cast to long already truncates, so drop the extra Round call.
    var minor = MinorUnits * multiplier;
    return new Money((long)minor, Currency);
}
```

The comment's premise is true about *what the cast does* (`(long)` on a `decimal` does
truncate toward zero) but wrong about *whether that's the same result as rounding*. For a
positive `decimal` value with a fractional part `< 0.5`, truncation and
`MidpointRounding.AwayFromZero` agree. For a fractional part `>= 0.5`, they disagree by
exactly 1: rounding rounds up, truncation drops the fraction. `(long)` never rounds up,
so truncation is always `<=` proper rounding for positive values — this is exactly why
finance's report is one-directional ("never in our favor").

`Program.cs`'s `CalculateMetrics` computes each annual plan's MRR contribution with:

```csharp
var monthlyAmount = plan.Cycle == BillingCycle.Annual
    ? plan.BasePrice.Multiply(1m / 12m)
    : plan.BasePrice;
```

`1m / 12m` is a non-terminating fraction (`0.08333...`), so for the overwhelming
majority of annual `BasePrice` values, `MinorUnits * (1/12)` lands on a fractional minor
unit — and roughly 5/12 of possible cent values (any whose remainder is `>= 0.5`) will
be truncated down by exactly one minor unit versus properly rounded. Every affected
annual customer silently loses 1 cent from the reported MRR, every single time the
dashboard is computed (this isn't a stored/accumulating error — `CalculateMetrics` runs
fresh on every `GET /api/dashboard` call — it just reappears identically every time, which
is why finance's weekly number tracks the annual-customer count instead of drifting with
time or request volume).

Monthly-cycle plans never call `Multiply` in this code path at all (`plan.BasePrice` is
used directly), which is exactly why the bug tracks annual customers one-for-one and
never shows up for monthly ones.

## Why the report is one-directional and scales with annual-plan count

- **One-directional**: truncation of a positive decimal never rounds up; proper rounding
  sometimes does. Truncated result `<=` correctly-rounded result, always. Hence "never in
  our favor."
- **Scales with annual-plan count, not time or traffic**: the computation is stateless
  and re-run on every request; each annual customer independently contributes at most 1
  minor unit of error, so the total error is bounded by (roughly `5/12` of) the number of
  annual customers, not by how long the bug has been live or how many times the dashboard
  has been loaded.

## Regression test

A naive test using values that divide evenly by 12 (e.g. `$1,200.00/year → $100.00/mo`)
would **not** catch this — `120000 / 12 = 10000.0` exactly, no fractional remainder, so
truncation and rounding agree. The test has to pick a `BasePrice` whose minor-unit value
is *not* a multiple of 12, and specifically one whose remainder is `>= 6/12`:

```csharp
[Fact]
public void Multiply_RoundsAwayFromZero_ForFractionalMinorUnits()
{
    // 29900 minor units / 12 = 2491.6666... -> should round UP to 2492, not truncate to 2491.
    var price = new Money(29_900, "USD");
    var monthly = price.Multiply(1m / 12m);
    Assert.Equal(2492, monthly.MinorUnits);
}
```

Also worth a property-style test: for a range of `MinorUnits` values, assert
`Multiply(1m/12m).MinorUnits` is always within 1 of `MinorUnits / 12m` and never more than
that — this is the kind of test that catches "quietly wrong by a little" bugs that exact
equality tests on round numbers miss.

## Fix

Restore the explicit rounding:

```csharp
public Money Multiply(decimal multiplier)
{
    var minor = decimal.Round(MinorUnits * multiplier, 0, MidpointRounding.AwayFromZero);
    return new Money((long)minor, Currency);
}
```

`decimal.Round` is not a meaningful performance cost here (this runs once per plan per
dashboard load, not in a hot per-request loop over millions of rows) — the "profiling"
justification in the incident's commit message doesn't hold up, and even if it were a
real hot path, correctness has to come before a micro-optimization on money math.

## Postmortem (model)

A one-line "simplification" of `Money.Multiply` removed `decimal.Round(...,
MidpointRounding.AwayFromZero)`, relying on the implicit truncation of the `(long)` cast
instead. Truncation and proper rounding agree for the majority of round-number test cases
an author might try by hand (e.g. whole-dollar amounts, evenly-divisible-by-12 annual
prices), which is almost certainly why this shipped without anyone noticing in manual
testing — the bug is invisible unless you pick an input with a fractional minor-unit
result whose remainder crosses the 0.5 boundary. It surfaced in production not as an
error or a crash but as a silent, systematic under-count that only a human doing manual
reconciliation against a second source of truth (finance's spreadsheet) caught, and only
noticed because they track the number precisely to the cent. The same `Multiply` method
is also used by `Coupon.CalculateDiscount` for percent-off coupons
(`training/_answers/review.md` finding 11 is adjacent to this) — the same rounding-down
bias would also make percent-off coupon discounts too small by up to a cent, silently
overcharging customers by the same mechanism, which nobody had reported yet at the time
of this incident. The general lesson: any function that both (a) does money arithmetic
and (b) has zero test coverage is a standing invitation for exactly this kind of "looks
like a safe cleanup" regression — money math needs tests that exercise non-trivial
fractional inputs specifically, not just round numbers.
