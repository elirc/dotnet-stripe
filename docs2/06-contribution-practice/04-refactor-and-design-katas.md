> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/06-contribution-practice/04-refactor-and-design-katas.md.txt).

# 06.04 — Refactor & Design Katas

Six katas that sharpen specific muscles. Each is self-graded; do them in a scratch branch or on paper.
They're smaller than the senior projects but exercise the same judgment.

---

## Kata 1 — Fix a boundary leak (money formatting)
**The leak:** the UI re-implements money formatting ([App.tsx formatMoney L394-L396](../../src/Billing.Web/src/App.tsx#L394-L396))
with its own `minorUnits / 100`, even though the server already sends `formatted`/`majorUnits` on
`MoneyDto` ([Responses.cs L5-L9](../../src/Billing.Api/Contracts/Responses.cs#L5-L9)).
**Task:** make the UI use server-provided formatting, OR make every money field a `MoneyDto` so the UI
never formats. Pick one and justify.
**Self-grade — Basic:** UI uses `formatted` for invoice totals. **Solid:** you identify *why* both exist
(raw-entity `Plan.basePrice` lacks `formatted`). **Strong:** you make the boundary consistent (all money
crosses as `MoneyDto`) and note this also fixes the 2-decimal assumption duplication (R-10).

---

## Kata 2 — Make the outbox retry path reachable
**The problem:** the backoff/requeue branch in [`WebhookDeliveryWorker.Deliver` L41-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L41-L53)
is dead because the `try` body does no fallible I/O (R-06).
**Task:** introduce an `IWebhookSender` seam so a test can force a failure and assert the backoff fires.
Don't implement real HTTP — just make failure *possible and testable*.
**Self-grade — Basic:** you add the interface and inject it. **Solid:** a test forces a throw and asserts
`Status == Failed` + `AvailableAt` advanced. **Strong:** you also clear `LastError` on success and note
the enqueue should be transactional with the business write (S-1 dependency).

---

## Kata 3 — Extract the `IBillingStore` seam
**The problem:** the engine depends on the concrete [`BillingStore`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5);
swapping persistence means touching every call site.
**Task:** extract an `IBillingStore` interface and depend on it in the engine + endpoints. Don't change
behavior — pure refactor.
**Self-grade — Basic:** the interface compiles and DI binds it. **Solid:** the engine + workers depend on
the interface; the concrete class is referenced only in `Program.cs`. **Strong:** you note this is the
prerequisite for [Comparison 1's migration](../03-architecture-and-patterns/07-comparative-implementations.md)
and that the interface surface (which methods leak persistence details?) is itself a design decision.

---

## Kata 4 — Kill stringly-typed data
**The problem:** `InvoiceLine.Kind` ([L76](../../src/Billing.Api/Domain/BillingModels.cs#L76)) and
`WebhookEvent.EventType`/`Destination` ([L113-L115](../../src/Billing.Api/Domain/BillingModels.cs#L113-L115))
are bare strings.
**Task:** make `Kind` an enum (see GF-7) and consider whether `EventType` should be one too. Keep the
wire serialization stable.
**Self-grade — Basic:** `Kind` is an enum, serialized as a string. **Solid:** you decide `EventType`'s
fate with a reason (it's an open set of provider event names → maybe stays a string). **Strong:** you
articulate the rule: enum when the set is *closed and ours*; string when it's *open or external*.

---

## Kata 5 — Design the dunning state machine (a migration on paper)
**The problem:** failed payments leave invoices `Open` forever; `PastDue`/`Uncollectible` are defined but
never reached (R-11).
**Task:** design the state machine: `Open → PastDue → (retry) → Paid | Uncollectible`. Specify the
transitions, who triggers them, the retry schedule, and which webhooks fire. Write it as a one-page RFC.
**Self-grade — Basic:** you draw the states. **Solid:** you specify the transition triggers and the
subscription-status interplay (`Active → PastDue → Canceled`). **Strong:** you include the migration
(backfill existing `Open` invoices), the test plan, and the rollback, and you note the dependency on
real payment failure (S-3) since the worker always succeeds today.

---

## Kata 6 — Review a flawed PR
**Task:** do [04/04 Review Kata 4 (refund endpoint)](../04-code-reading-gym/04-review-katas.md) as a full
review: write every finding with a severity label, an anchor, and a kind tone, per
[07/01](../07-career-and-collaboration/01-code-review-mindset.md).
**Self-grade — Basic:** you catch the bare-catch bug. **Solid:** you also catch the non-idempotency and
the missing auth, labeled and anchored. **Strong:** your review teaches the author, offers to pair, and
notes what the PR did right — and you'd be comfortable posting it on a real teammate's PR.

## Verification Notes

Every kata anchors to read-verified code (2026-06-09) and a confirmed risk-register entry. Katas 5 and 6
are design/paper exercises; the rest are scratch-branch refactors. Nothing here changes product code in
this repo.
