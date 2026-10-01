> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/05-quality-engineering/01-testing-strategy.md.txt).

# 05.01 — Testing Strategy

Current state: **there is no test project and no test anywhere** (verified by file listing; R-03). So
this page is the strategy you'd adopt, sized to *this* repo, and what to write first.

## What belongs where

| Level | Tool | What to test here | Examples from this repo |
| --- | --- | --- | --- |
| **Unit** | xUnit | Pure logic with the injected clock — money math, the discount rule, invoice arithmetic | [`Money`](../../src/Billing.Api/Domain/Money.cs), [`Coupon.CalculateDiscount`](../../src/Billing.Api/Domain/BillingModels.cs#L31-L49), [`BillingEngine.GenerateInvoice`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L113) |
| **Integration (in-proc)** | `WebApplicationFactory<Program>` | The HTTP surface end to end against the in-memory store — validation, status codes, idempotency replay | every endpoint in [Program.cs](../../src/Billing.Api/Program.cs#L37-L250) |
| **Background/async** | xUnit + a controllable clock | Worker processing a queued job; webhook delivery | [`PaymentWorker`](../../src/Billing.Api/Infrastructure/PaymentWorker.cs), [`WebhookDeliveryWorker`](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs) |
| **Contract** | (manual today) | The C# DTO ↔ TS type match | [Responses.cs](../../src/Billing.Api/Contracts/Responses.cs) vs [api.ts](../../src/Billing.Web/src/api.ts) |
| **UI** | React Testing Library | Component state transitions, form submits | [App.tsx](../../src/Billing.Web/src/App.tsx) |
| **E2E** | Playwright | The full "create → run cycle → pay" journey | the whole app |

For a repo this size, the **highest-value 80%** is: unit tests on the engine (deterministic via the
clock) + a thin layer of `WebApplicationFactory` integration tests. UI/E2E is lower priority for a
single screen.

## The two seams that make this testable

1. **`IBillingClock`** — inject a fixed clock and the invoice arithmetic becomes deterministic. This is
   the single most important testing affordance in the repo.
2. **The self-seeding store** — `BillingStore` seeds known data with **fixed GUIDs**
   ([Seed L267-L321](../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L321)), so you can write
   assertions against the seeded Scale subscription `062a6a8f-…` without setup. The seed *is* your
   fixture.

> **Caveat (a real gotcha):** the seed uses `now.AddDays(-30)` etc. relative to construction time, and
> `Seed` is called with `clock.UtcNow` ([store L21](../../src/Billing.Api/Infrastructure/BillingStore.cs#L21)).
> With a *fixed* clock, the seed dates are deterministic; with the system clock they shift. Use a fixed
> clock in tests or your date assertions will flake.

## What NOT to test

- **The framework.** Don't test that ASP.NET routes `/api/customers` — test *your* handler logic.
- **Trivial DTO mapping** like [`MoneyDto.From`](../../src/Billing.Api/Contracts/Responses.cs#L7-L8)
  beyond one smoke check — it's a constructor call.
- **The in-memory store's dictionary semantics.** Don't test that a `Dictionary` stores a value; test
  the *business rules* layered on it (email uniqueness, unbilled-usage selection).
- **Getters/records.** Records' equality is the compiler's job.

## Flake prevention (the usual suspects, located here)

| Source of flake | Where it would bite | Fix |
| --- | --- | --- |
| **Wall-clock time** | the seed's relative dates; the verifier's `DateTimeOffset.UtcNow` ([L25](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L25)) | inject a fixed `IBillingClock`; refactor the verifier to use it |
| **Random GUIDs** | `Guid.NewGuid()` in invoice/payment ids | assert on *shape*/count, not exact ids; or inject an id factory |
| **Timer timing** | worker `PeriodicTimer` intervals (4s/8s) | don't wait on the real timer — call `Process`/`Deliver` directly, or drain the queue synchronously |
| **Shared singleton store** | tests sharing one `WebApplicationFactory` mutate the same store | build a fresh factory per test class, or reset state between tests |
| **Ordering** | list accessors sort by `CreatedAt` ([store L26](../../src/Billing.Api/Infrastructure/BillingStore.cs#L26)) | assert on sets, not positions, unless order is the thing under test |

## A pragmatic test pyramid for this repo

```
        ▲  few   E2E (Playwright): one "create→cycle→pay" smoke   ← later
       ───
      ─────  some Integration (WebApplicationFactory): validation, idempotency, status codes
     ───────
    ─────────  many Unit (engine + Money + Coupon, fixed clock)   ← start HERE
```

## Drill

Write the *list* of the first 8 tests you'd add, in priority order, each one sentence. **Self-grade —
Basic:** you list happy-path engine tests. **Solid:** you include the EUR-currency failure (R-01) and
an idempotency replay test. **Strong:** your ordering front-loads the tests that pin the riskiest
behavior (money totals, double-charge) and you note which need a fixed clock.

## Verification Notes

The "no tests exist" claim is verified by the full file listing (no `*.Tests.csproj`, no test
framework reference). The seams and flake sources are read-verified from the anchored code on
2026-06-09.
