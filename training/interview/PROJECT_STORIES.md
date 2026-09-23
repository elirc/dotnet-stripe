# Interview stories — SubscriptionBillingDashboard (.netstripe)

Complete these after doing the review + at least one incident. Each is a STAR
(Situation/Task/Action/Result) skeleton tied to a specific artifact in this repo — fill in
your own words, keep the file references so an interviewer can ask you to pull up the
code.

## 1. Debugging story — the idempotency-caches-failures incident
- **Situation**: `training/incidents/001-idempotency-caches-failures.md` — what did the
  support tickets actually say, and what was the first misleading signal?
- **Task**: what were you actually asked to find (not "fix the code" — what was the
  business-visible symptom you had to explain)?
- **Action**: what's the one HTTP response header (`IdempotencyMiddleware.cs:46`) that
  cracked the case once you noticed it? Walk through how you'd explain finding it to
  someone who hasn't seen the code.
- **Result**: what's the actual one-line fix, and why is "cache failures too" the wrong
  instinct even though the original commit message sounded reasonable?

## 2. Trade-off story — why cache 2xx only, and what that costs
- Fill in after incident 001. The fix reintroduces a real gap: a client that *did*
  succeed but never saw the response (network drop) has no way to know it succeeded and
  might resubmit a duplicate. Explain the trade-off you'd defend in an interview: why is
  "sometimes a legit success gets silently retried" a smaller problem than "every failure
  is permanently unretriable"? What would you build next if you had one more sprint
  (idempotent writes at the storage layer — `training/_answers/review.md` finding 1)?

## 3. Incident story — the MRR rounding drift
- **Situation**: `training/incidents/002-mrr-rounding-drift.md` — a discrepancy nobody
  would have found without a human manually reconciling against a second source of truth.
- **Task**: what made this bug invisible in ordinary manual testing? (Hint: which inputs
  make truncation and proper rounding produce the same answer, and why would a developer
  testing by hand gravitate toward exactly those inputs?)
- **Action**: describe the regression test you wrote and *why* that specific test input
  (not a round number) is what actually pins the bug down.
- **Result**: what's the blast radius once you realized `Money.Multiply` is also used by
  `Coupon.CalculateDiscount` (`training/_answers/review.md` finding 11)? How would you
  communicate "this bug is bigger than the ticket that reported it" to a PM without
  sounding like you're scope-creeping the fix?

## 4. Agent-caught-bug story — the repo didn't even compile
- **Situation**: `training/_answers/review.md` finding 0 — two separate compile errors on
  a clean checkout of `main`, despite ~90+ files of curriculum describing this codebase in
  detail.
- **Task**: what's the very first thing you now do before trusting any "this looks
  done" codebase, whether it came from a course, an agent, or a teammate's PR?
- **Action**: name the two exact errors and the one-line fix for each.
- **Result**: what does this tell you about how much weight to put on documentation
  volume/quality as a signal for whether code actually works?

## 5. Money/domain-knowledge story — integer minor units
- Why does `Money` (`Domain/Money.cs`) store `MinorUnits` as a `long` instead of using
  `decimal` directly for dollars? Be ready to explain float/decimal drift with a concrete
  example, and why `Money.FromMajor` rounds with `MidpointRounding.AwayFromZero` rather
  than the runtime default (banker's rounding, `MidpointRounding.ToEven`) — is that the
  right choice for a billing system? What would change your answer?
