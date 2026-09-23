# Incident 002 — dashboard MRR is quietly lower than finance's numbers

**Branch**: `training/incidents/002-mrr-rounding-drift` (commit `bfce8de`, off `main`
`a84f11b`)

## What's being reported

Finance reconciles the `/api/dashboard` `metrics.monthlyRecurringRevenue` figure against
their own spreadsheet weekly. This week's note in the shared channel:

> "MRR from the API is $0.07 under our number again. Same as last week (was $0.05 under
> then, and $0.01 the week before that — we only had one annual customer back then).
> Nothing changed on our end, we're summing the same subscriptions the same way we always
> have. Is the app rounding something wrong? It's small but it's never in our favor and
> it's never exactly zero, and it only ever seems to move when we sign up another annual
> customer."

The seeded data (`BillingStore.Seed`) only has two `BillingCycle.Monthly` subscriptions,
so it won't reproduce the gap by itself. To see it:

1. `POST /api/plans` a new plan with `"cycle": "Annual"` and a `basePrice` that doesn't
   divide evenly by 12 — e.g. `299.00`.
2. `POST /api/subscriptions` a customer onto that plan.
3. `GET /api/dashboard` and look at `metrics.monthlyRecurringRevenue` — compare it by
   hand to what `$299.00 / 12` should contribute per month.

## What we know so far

- The discrepancy is one-directional: the API number is always lower than or equal to a
  hand-computed number, never higher.
- It only moves when an annual-cycle plan/subscription is added — monthly-cycle
  customers never seem to contribute to the gap.
- `Money`, `BillingEngine`, and the dashboard metrics calculation are the only code
  anywhere near this number. Nothing about coupons or usage overage appears to be
  involved — the gap tracks the count of annual-cycle customers, and disappears entirely
  in an environment with none.

## Your task

1. Find the root cause. Trace `metrics.monthlyRecurringRevenue` back through
   `Program.cs`'s `CalculateMetrics` to wherever the actual arithmetic happens.
2. Explain *why* the error is one-directional and grows with the number of annual-cycle
   plans specifically, not monthly ones.
3. Write the regression test that would have caught this before it shipped — it needs a
   case that a naive "does 29+249=278 come out right" test would still pass while missing
   this bug entirely; think about what input makes the bug visible.
4. Write the fix.
5. One-paragraph postmortem, including what silent-money-drift bugs like this cost in
   practice if they aren't caught until a human manually reconciles against a spreadsheet.

No hints below this line. Sealed answer: `training/_answers/002-mrr-rounding-drift.md`.
