# Learn here: .netstripe (SubscriptionBillingDashboard)

This is the second stop on the B01 .NET route, after `.netramp`. It has the same "small backend, in-memory data, zero
tests" shape, but it adds money arithmetic, idempotency, webhooks and background workers. It also adds a new lesson:
**the code on `main` has never compiled.**

## What this app is and why it's in the portfolio
- A Stripe-style subscription billing API. It's written as .NET 8 **minimal APIs**: every endpoint is a lambda in
  `src/Billing.Api/Program.cs:37-250`, and there are no controllers. Behind it sit a `BillingEngine`
  (`Infrastructure/BillingEngine.cs`), an in-memory `BillingStore` with seed data (`Infrastructure/BillingStore.cs:267-321`),
  and two `BackgroundService` workers. A React dashboard lives in `src/Billing.Web`, which isn't in the `.sln`.
- It's in the portfolio because it concentrates three things that take real skill in production code:
  - integer minor-unit money (`Domain/Money.cs`);
  - HTTP idempotency keys (`Infrastructure/IdempotencyMiddleware.cs`);
  - a real payment result arriving by webhook while a simulator also writes results (`Infrastructure/PaymentWorker.cs`).
- Existing drill material is in `training/`: a blind review (19 findings), a characterization-test exercise, 2
  incidents and a mini interview kit. This `learn/` folder is the on-ramp to it.

## Known state of main
**`main` does not compile.** Source: `Desktop\opusorganize\batch1report\projects\netstripe.md` §Verification, and
`training/_answers/review.md` finding 0. There are two independent errors:
- **CS0246** in `src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs`: `IdempotencyStore(IBillingClock clock)` at
  line 90 needs `using Billing.Api.Domain;`, which the file doesn't have.
- **CS0173** at `src/Billing.Api/Program.cs:143`: `var amountOff = request.AmountOff is null ? null : Money.FromMajor(...)`.
  `Money` is a non-nullable `readonly record struct`, and `var` gives the conditional no target type.

The repo's own `README.md:66` admits nobody ever compiled it. The 93-file `docs/` and `docs2/` curricula were written
against this code without running it. Treat them as hypotheses, not facts
([05 Verify before trust](../../opusorganize/apprenticeship/curriculum/05-verify-before-trust.md)).

Both incident branches also carry the two one-line build fixes. On branch 001 they're bundled into `8b0c838`. On branch
002 the `Program.cs` fix rides in `dd2875b` and the `using` is its own commit, `bfce8de`. Both branches build with 0
errors on SDK 10.0.302. Those fixes aren't part of either puzzle. **This learn pass doesn't fix
`main`.** Session 1 makes you diagnose the errors and fix them on your own scratch branch.

## Prerequisites
1. **Everything in `Desktop\.netramp\learn\`** (DI lifetimes, request tracing, a first xUnit test). If you haven't done
   it, go back.
2. **Records and `with`, structs versus classes, `Nullable<T>`.** Read `src/Billing.Api/Domain/BillingModels.cs`
   (every entity is an immutable `sealed record`) and `Domain/Money.cs`. On-ramp exercises 1 and 3 check this.
3. **Minimal API parameter binding** (route, body and DI parameters in one lambda). Read `Program.cs:67-83` and
   `Program.cs:211-221`. On-ramp exercise 2 checks this.
4. **Idempotency keys and duplicate delivery.** Read [02 Idempotency](../../opusorganize/apprenticeship/curriculum/02-idempotency.md).
5. **Money rounding.** Read [20 Money and time](../../opusorganize/apprenticeship/curriculum/20-money-and-time.md), the money sections.
6. (Needed for Session 7) **Check-then-act races.** Read [03 Concurrency races](../../opusorganize/apprenticeship/curriculum/03-concurrency-races.md).

## Study route
Total: about 10–12 hours over 8 sessions. Keep both `learn/_answers/` and `training/_answers/` closed until the
session tells you to open them.

### Session 1 — A codebase that never compiled (60 min)
- **Files/branches:** `README.md:66`, `Program.cs:143`, `Infrastructure/IdempotencyMiddleware.cs:1-3,90`. Branch `main`, then your own `learn/build-fix` branch.
- **Do by hand:**
  - Run `dotnet build src/Billing.Api` and read both errors.
  - Explain each error in your own words *before* asking anyone (on-ramp exercise 1).
- **Delegate, then verify:**
  - Use `learn/01-agentic-practice.md` Drill A: have the agent make it compile with the minimum diff.
  - Check the diff against Drill A's checklist. It should be exactly 2 changed lines, with no warning suppression and no refactors.
- **Exit criterion:**
  - `dotnet build src/Billing.Api` on your branch shows 0 errors.
  - `git diff main --stat` shows 2 files and about 2 lines.
  - You can say why `(Money?)null` fixes CS0173 and `var` alone doesn't.

### Session 2 — On-ramp exercises 2–4 (75 min)
- **Files/branches:** `learn/00-onramp.md` exercises 2–4. Branch `learn/build-fix`.
- **Do by hand:** all of it. Do the arithmetic for exercise 4 on paper.
- **Delegate, then verify:** nothing.
- **Exit criterion:** your hand-computed invoice for the seeded `scale` subscription matches `learn/_answers/00-onramp.md` to the cent.

### Session 3 — On-ramp exercises 5–8 and the test project (90 min)
- **Files/branches:** `learn/00-onramp.md` exercises 5–8, then the setup block of `training/review/characterization-tests-billingengine.md`.
- **Do by hand:** the traces in 5–7, and the list of `Money` test cases in exercise 8.
- **Delegate, then verify:**
  - Have an agent create `src/Billing.Api.Tests` and add it to the `.sln`.
  - Check that it references `Billing.Api`, and that it builds only because of your Session 1 fix.
- **Exit criterion:**
  - Your `Money` tests pass.
  - Switching `MidpointRounding.AwayFromZero` to `ToEven` in a throwaway edit makes at least one of them fail. Revert afterwards. If none fail, your inputs never hit a midpoint; see exercise 8.

### Session 4 — Blind review (75 + 20 min)
- **Files/branches:** `training/review/EXERCISE.md`, then `training/_answers/COMPARE.md`, then `training/_answers/review.md`.
- **Do by hand:** the whole review. Don't read `docs/` or `docs2/` first.
- **Delegate, then verify:** afterwards, ask an agent for an independent review, and score it against the 19 sealed findings. Agents usually find finding 5 (no auth) and miss finding 3 (the simulator versus webhook contradiction). Check whether yours did.
- **Exit criterion:** you found at least 7 of the 19 findings, including finding 1 or finding 2.

### Session 5 — Characterize BillingEngine (90 min)
- **Files/branches:** `training/review/characterization-tests-billingengine.md`, then `learn/01-agentic-practice.md` Drill B.
- **Do by hand:**
  - Write the spec, which must require a fake `IBillingClock` and literal expected values.
  - Pick the seeded IDs yourself.
- **Delegate, then verify:** the agent writes the tests. Grade them with the Drill B checklist.
- **Exit criterion:** the tests are green on `learn/build-fix`. The hybrid-plan test asserts the numbers from on-ramp exercise 4 as literals.

### Session 6 — Incidents (2 × 45 min)
- **Files/branches:**
  - `training/incidents/001-idempotency-caches-failures.md` on branch `training/incidents/001-idempotency-caches-failures`.
  - `training/incidents/002-mrr-rounding-drift.md` on branch `training/incidents/002-mrr-rounding-drift`.
  - Use worktrees outside the repo, as shown in `training/README.md`.
- **Do by hand:** diagnose each incident from its brief. Separate the two bundled build-fix lines from the real change.
- **Delegate, then verify:** the fix and the regression test, via Drills C and D.
- **Exit criterion:** your root cause matches `training/_answers/00N-*.md`. Each regression test fails on the incident branch and passes after the fix.

### Session 7 — Your first design ticket: webhook dedup (75 min)
- **Files/branches:** `training/tickets/M1-webhook-event-dedup.md` (added in B08). Do it on `learn/build-fix`.
- **Do by hand:**
  - Write the design: where the dedup check goes, what is stored, and what happens inside the lock.
  - Write the concurrent test case.
- **Delegate, then verify:** the implementation. Reject any version that checks, then processes, then marks.
- **Exit criterion:** delivering the same `EventId` twice, both sequentially and in parallel, results in exactly one new `Payment` and one queued outbound webhook.

### Session 8 — Interview kit (45 min)
- **Files/branches:** `training/interview/PROJECT_STORIES.md`, `training/interview/TECHNICAL_QUESTIONS.md`.
- **Do by hand:** the stories, based on real work from Sessions 1–7. Story #4, "agent-caught-bug", should be your Session 1.
- **Delegate, then verify:** use an agent as a mock interviewer that asks follow-up questions about money and idempotency.
- **Exit criterion:** you can answer TQ 2, 4, 5 and 7 without notes.

## Do vs. delegate (summary)
| Always by hand | Delegate, then verify |
|---|---|
| Reading compiler errors and explaining them | The 2-line build fix, on a branch |
| Money arithmetic, traces, predictions | Test-project scaffolding and test bodies from your spec |
| Blind review and incident diagnosis | Fixes for the incidents and M1 |
| Designing the dedup and choosing lock placement | A second-opinion review, which you then grade |

## How you know you're done here
- You have a branch where `main` builds, with a `Billing.Api.Tests` project that characterizes `BillingEngine` and
  `Money`, and both incidents' regression tests.
- You can explain, without notes:
  - why 2xx-only caching is a deliberate trade-off;
  - why `decimal` to `long` truncation hides behind round test inputs;
  - why `EventId` dedup must be "mark and process atomically".

## Next repo
**`Desktop\netauth`.** It's an OIDC identity provider, and it's the security specialty of this route. It's another
`main` that doesn't compile, this time because of a hallucinated EF Core API, and it adds an SDK-pinning problem.
