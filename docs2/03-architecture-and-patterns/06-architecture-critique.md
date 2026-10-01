> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/03-architecture-and-patterns/06-architecture-critique.md.txt).

# 03.06 — Architecture Critique

A senior review of this codebase as if you were asked, "should we build on this?" The repo is
**synthetic** (no history, uniform style), so this critique is deliberately aggressive: generated code
that *looks* finished is where unstated assumptions hide. Every claim is anchored or labeled a
hypothesis.

## What this codebase gets genuinely right

1. **Money is a value object with integer minor units.** [`Money`](../../src/Billing.Api/Domain/Money.cs#L5-L68)
   is the best decision in the repo — it makes invoice math deterministic and currency mixups loud.
   This is the thing most billing prototypes get wrong, and it's right here.
2. **Time is injected.** [`IBillingClock`](../../src/Billing.Api/Domain/BillingClock.cs#L3-L11) gives
   the whole engine a deterministic-test seam. (Marred only by the verifier bypassing it — fixable.)
3. **The domain has no downward dependencies.** Records + `Money` + clock know nothing about HTTP or
   storage, so the engine is portable and the store is *replaceable* — which is the entire premise.
4. **Clean exception→HTTP translation.** The engine throws domain exceptions; endpoints map them with
   `catch … when` filters ([e.g. L170-L173](../../src/Billing.Api/Program.cs#L170-L173)). Concerns stay
   separated.
5. **Modern, correct async.** `PeriodicTimer`, `CancellationToken` propagation, no sync-over-async.

## The risks, prioritized (worst first)

| Rank | Risk | Why it's ranked here | Confidence |
| --- | --- | --- | --- |
| 1 | **No auth / tenant isolation** ([R-02](../08-reference/risk-register.md)) | It's not a bug, it's a missing *dimension*; everything else is moot if anyone can read/mutate anyone's billing. | Confirmed |
| 2 | **Currency coupling** ([R-01](../08-reference/risk-register.md)) | A first-class field (currency) can take down the whole dashboard; latent because the seed is all-USD. | Confirmed |
| 3 | **Non-idempotent payment application** ([R-05](../08-reference/risk-register.md)) | Duplicate payments are a money/accounting problem, and provider webhooks *will* retry. | Confirmed |
| 4 | **No catch-up billing** ([R-07](../08-reference/risk-register.md)) | Silent revenue loss on missed cycles. | Confirmed |
| 5 | **Idempotency race + restart amnesia** ([R-04](../08-reference/risk-register.md)) | The double-charge guard has a hole. | Confirmed |
| 6 | **Theatrical reliability** ([R-06](../08-reference/risk-register.md)) | The retry/outbox code is unreachable; it *looks* robust and isn't. | Confirmed |
| 7 | **No tests** ([R-03](../08-reference/risk-register.md)) | Multiplies every risk above — nothing fails on regression. | Confirmed |

The synthetic-repo tell: items 2, 3, 5, 6 are exactly the class of defect that generated code hides —
the happy path is immaculate, the concurrency/retry/multi-currency edges are unfinished but *dressed
up* (backoff code, currency fields) to look finished. Don't be reassured by surface polish.

## Strongest choice vs biggest regret, in one line each

- **Strongest:** integer `Money`. It will still be right at 10,000× the scale.
- **Biggest regret (if shipped as-is):** the metric math living in `Program.cs` with a hardcoded
  `"USD"` ([CalculateMetrics L262](../../src/Billing.Api/Program.cs#L262)) — a business rule in the
  wrong layer that turns a data variation into an outage.

## If I owned this for three months

A sequenced plan, each step with a migration + test strategy. Order is deliberate: make it *safe to
change* before changing much.

**Weeks 1–2 — Build the safety net (unblocks everything).**
- Stand up an xUnit project; write characterization tests pinning current behavior (Flow 2 = `$1,111.20`,
  the discount cap, the idempotency replay/409). *Migration:* none. *Test:* this IS the test work.
- Add a `WebApplicationFactory` smoke test per endpoint.
- *Why first:* every later change is reckless without a regression net (R-03).

**Weeks 3–4 — Close the money-correctness gaps.**
- Fix currency coupling (R-01): introduce a reporting currency or group metrics by currency; constrain
  subscription currencies. *Migration:* none (logic). *Test:* the EUR-plan dashboard test that's red today.
- Make payment application idempotent (R-05): dedup on `EventId`/`providerReference`. *Migration:* add a
  processed-events set/table. *Test:* "apply twice → one payment"; "replay webhook → one effect".
- Fix catch-up billing (R-07): loop while `CurrentPeriodEnd <= billingDate`. *Test:* "3 overdue periods → 3 invoices".

**Weeks 5–8 — Persistence + auth (the big rocks).**
- Migrate the store to EF Core behind an `IBillingStore` seam (the
  [Comparison 1 migration drill](07-comparative-implementations.md)), with real transactions around
  multi-write ops and the indexes the in-memory version never needed. *Migration:* schema + the
  captive-dependency lifetime fix. *Test:* characterization tests must stay green across the swap;
  flag-based rollback.
- Add authentication + tenant scoping (R-02) as a non-bypassable filter/store scope. *Migration:*
  `TenantId` column + backfill. *Test:* cross-tenant access returns 404; every store query is scoped.

**Deferred (deliberately):** dunning/`PastDue` lifecycle (R-11), real webhook HTTP + reachable backoff
(R-06), multi-decimal currency (R-10). These are real but lower-leverage than money-correctness and
the persistence/auth foundation.

## Confirmed vs hypothesis

- **Confirmed (read-verified code paths):** all of R-01..R-12. The control flow demonstrably does
  these things.
- **Hypothesis (needs a runtime check):** the *exact* failure surface of R-01 (does the dashboard 500,
  or does the exception get swallowed somewhere?) — I believe it's an unhandled 500 because there's no
  global exception handler registered ([Program.cs](../../src/Billing.Api/Program.cs#L32-L37) has no
  `UseExceptionHandler`), but I did not run it. Confirm before quoting it as fact.

## What I would *not* change

- The in-memory store *for its stated purpose* — it's the right call for a learning repo and it's
  cleanly replaceable (Comparison 1 verdict).
- Minimal APIs at this size (Comparison 2 verdict).
- Hand-rolled client fetch for one screen (Comparison 3 verdict).

Knowing what *not* to touch is as senior as knowing what to fix. A rewrite here would be malpractice;
the bones are good.

## Critique kata

You have one sprint and can fix exactly one risk. Which, and how do you defend *not* fixing the others
this sprint? **Self-grade — Basic:** you pick a real risk. **Solid:** you pick R-03 (tests) or R-01
and justify by leverage. **Strong:** you argue sequencing — tests first because they make the rest
safe — and you explicitly defer auth/persistence with a reason, showing you optimize for *risk
reduction per unit of change*, not for closing the most tickets.

## Verification Notes

Strengths and risks are read-verified on 2026-06-09. The single hypothesis (R-01's exact runtime
surface) is labeled as such because no global exception handler was found but the code was not
executed. Rankings are judgment, not measurement.
