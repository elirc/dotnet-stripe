# 07.01 — Code Review Mindset

Reviewing is where you practice judgment on someone else's code with low stakes. The five questions,
in order — stop early only when an earlier one fails hard.

| # | Question | What you're checking |
| --- | --- | --- |
| 1 | **Does it work?** | Meets the stated requirement; happy path correct. |
| 2 | **Is it correct?** | Edge cases, error paths, concurrency, money/rounding. |
| 3 | **Will it stay correct?** | Tests, invariants, will the next change break it silently? |
| 4 | **Does it fit?** | Follows the repo's existing patterns and boundaries. |
| 5 | **Is it kind to maintainers?** | Readable, named well, no surprises, documented where non-obvious. |

## Repo-specific review checklist

When reviewing a change to *this* codebase, check the things this codebase specifically gets wrong:

- [ ] **Money:** does new math go through [`Money`](../../src/Billing.Api/Domain/Money.cs#L5) (integer,
  currency-checked), or does it reintroduce `decimal`/`double` or a `/ 100` (R-10)?
- [ ] **Currency:** does it assume USD? Any new aggregate must not seed `Money.Zero("USD")` blindly
  (R-01).
- [ ] **Idempotency:** does a new mutating endpoint rely on the middleware, and is the new engine
  operation idempotent if retried (R-05)?
- [ ] **Auth:** does it read/mutate by id without an ownership check? (Everything does today — don't
  add more IDOR; R-02.)
- [ ] **The clock:** does new time logic take `IBillingClock`, or did it sneak in `DateTime.UtcNow`
  like the [verifier](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L25) did?
- [ ] **Boundaries:** is new logic in the right layer (engine, not `Program.cs`; not the UI)?
- [ ] **Tests:** is there a test? (There are none today — a new one is a gift; R-03.)
- [ ] **The store:** does it read a dictionary directly, or go through a locked accessor?
- [ ] **DTOs:** does a renamed entity field break the raw-entity dashboard payload + [api.ts](../../src/Billing.Web/src/api.ts#L16-L143)?

## Severity language (so the author knows what's blocking)

- **Blocking** — must fix before merge (correctness, security, data loss).
- **Important** — should fix, but could be a fast follow with a ticket.
- **Optional / nit** — preference; the author decides.

Always label. An unlabeled comment makes the author guess whether you're vetoing.

## Example comments (good vs bad)

**Bad (vague, no anchor, no severity):**
> "This looks wrong, money is hard."

**Good (anchored, specific, kind, labeled):**
> **Blocking.** This sums plan prices with `Money.Zero("USD")` as the seed, same shape as
> [`CalculateMetrics` L262](../../src/Billing.Api/Program.cs#L262). If any active subscription's plan is
> non-USD, `Money.Add` throws ([Money L65](../../src/Billing.Api/Domain/Money.cs#L65)) and the endpoint
> 500s. Can we group by currency or use a reporting currency? Happy to pair on it.

**Good (asking, not accusing — for an uncertain finding):**
> **Important (investigate).** I think the retry branch here can't be reached because the `try` body
> doesn't do real I/O — same as [WebhookDeliveryWorker L28-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L28-L53).
> Could you confirm there's a path that actually throws? If not, a test that forces a failure would
> prove the backoff works.

**Good (optional nit):**
> **Nit (optional).** `Kind` is a bare string here and in [InvoiceLine L76](../../src/Billing.Api/Domain/BillingModels.cs#L76).
> An enum would prevent typos, but it's consistent with the existing code, so your call.

## Review kata

Open [04/04-review-katas.md](../04-code-reading-gym/04-review-katas.md) and review one fake PR. Write
each finding with a **severity label**, an **anchor to the pattern it resembles**, and a **kind tone**.
**Self-grade — Basic:** you find the headline bug. **Solid:** you label severity and anchor it.
**Strong:** your comment teaches the author *why*, offers help, and you also note what the PR did
*well* (reviews that are all-negative get ignored).

## Verification Notes

Checklist items map to read-verified anchors and risk-register entries (2026-06-09). Example comments
are illustrative review prose; the code facts inside them are anchored and accurate.
