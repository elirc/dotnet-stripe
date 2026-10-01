# Billing workshop: the learning route

This course turns the subscription billing dashboard into a junior-to-mid-level CRUD application workshop. You work with customers, plans, coupons, subscriptions, usage events, invoices and payment attempts. The objective is to explain what a change does across the browser, HTTP boundary, domain rules and database, and then prove that explanation with tests.

The application is a **local simulator**. It never charges a card, sends an email or delivers an external webhook. Its payment queue and outbox are durable, but processing is explicit: use the simulator controls to choose a successful or declined batch. This makes intermediate states visible and failure exercises repeatable. The custom signed callback lab is not compatible with Stripe's real API.

## Start the application

Install the .NET 10 SDK and Node 22. The SDK selection is in [global.json](../global.json); JavaScript dependencies are locked in [package-lock.json](../src/Billing.Web/package-lock.json). From the project root, open two terminals:

```powershell
# Terminal 1
./scripts/run-api.ps1

# Terminal 2
cd src/Billing.Web
npm.cmd ci --ignore-scripts --no-audit --no-fund
npm.cmd run dev
```

Open `http://127.0.0.1:5173`. Choose **Create a new workspace**. Use a fictional email, a workspace code such as `billing-lab`, and a unique password of 12–128 characters. A new workspace starts empty. Browser sessions are kept in memory and expire after eight hours. Reloading the page requires sign-in; the SQLite records remain.

The API normally creates `src/Billing.Api/App_Data/billing-learning.sqlite`. To use another location, set `Billing__Database` before starting the API. Choose a **new** path for an isolated exercise. The application refuses an existing file with an unknown application marker or schema version. It does not reset or migrate that file automatically. Never point a test at a database you need to keep.

For one local packaged application, run [publish-local.ps1](../scripts/publish-local.ps1). From `.artifacts/publish`, run `dotnet Billing.Api.dll --urls http://127.0.0.1:5088` and open that address. This creates a local package, not a deployment. Its database is separate unless you explicitly configure the same path.

## Your first complete workflow

1. Add customer **Ada Studio**, `ada@example.test`, currency USD.
2. Add plan **Studio Hybrid**, code `studio-hybrid`, Monthly, Hybrid, base price 20, included units 100, extra unit price 0.10, metric `api_calls`.
3. Start a subscription for that customer and plan. Inspect it and note its current period boundaries.
4. Add a usage event with quantity 150 and metric `api_calls`. Its default time is the current simulator time. The event must fall inside the subscription period.
5. Advance the simulator to the subscription's exact period end. Inspect the invoice: base fee 20 plus 50 extra units at 0.10 gives **USD 25.00**.
6. Queue a simulated payment from the invoice details. Inspect Payments before processing: one attempt is Pending.
7. Process as decline, inspect the failed attempt, then queue a new attempt and process as success. The invoice becomes Paid; both attempts remain in history.

Do not skip the inspection between steps. Being able to predict the next state is more valuable than clicking through quickly.

## Prerequisites and sequencing

**Read this before chapter 01.** This is not a first course.

**You need already:** C# classes, records, `async`/`await`, and LINQ well enough to read a method without looking things up; enough SQL to read a `SELECT ... WHERE`; the shape of an HTTP request and response including status codes and headers; and React/TypeScript to the level of reading a component, not writing one from scratch. You should have completed **one** course on validation-and-ordering discipline first — something where a single workflow is repaired and proved with tests. Chapters 01–04 assume you already believe that a rejected request must leave no trace, and spend their time on *how* rather than *why*.

**You do not need:** prior experience with ETags, optimistic concurrency, idempotency keys, transactional outboxes, HMAC signing or multi-tenant authorization. Those are what this course teaches. But it teaches all five of them **at once**, because that is how they appear in the code — and that is the single hardest thing about this material. If you find chapter 02 heavy, that is the material, not you. Do Part 1 and come back.

**Toolchain:** .NET 10 SDK (selected by [global.json](../global.json)) and Node 22. On macOS or Linux, drop the `.cmd` from the `npm.cmd` commands above and start the API with `dotnet run --project src/Billing.Api` instead of the PowerShell script; everything else is identical.

### Part 1 — workflow, CRUD, pagination, validation

Do this first and stop at the end of it. If you learn nothing else here, learn this half properly.

| Guide | What you should be able to explain afterward | Estimate |
|---|---|---|
| [Codebase map](01-CODEBASE-MAP.md) | Where a browser edit becomes a database transaction | 60–75 min |
| [Concepts, part 1](02-CONCEPTS.md) — money, tenancy, time | What a `Money` value is, why currency is part of it, and why billing time is not wall-clock time | 60 min |
| **Exercise 0** in [Practice](05-PRACTICE.md) | Which tests defend one invariant, having removed it and counted them | 45 min |
| [Worked change](03-WORKED-CHANGE.md) | How an implemented customer edit protects history | 60 min |
| [Testing and debugging](04-TESTING-AND-DEBUGGING.md) | Which layer can prove which claim | 60 min |
| [Practice](05-PRACTICE.md) exercises 1–4 | A rule explained and tested, a field added end to end, a filter added, a projection improved | 1 session each |

Part 1 is roughly a full day of reading plus four working sessions. Exercises 1–4 are ordinary junior work: you change code, you add a test, you know when you are done.

### Part 2 — concurrency, receipts and the outbox

Come back to this after Part 1 has settled, ideally after you have shipped something at work that uses one of these ideas.

| Guide | What you should be able to explain afterward | Estimate |
|---|---|---|
| [Concepts, part 2](02B-CONCEPTS-CONCURRENCY.md) — concurrency, receipts, transactions | Stale intent vs. repeated intent vs. partial intent, and why each needs a different mechanism | 75–90 min, reread |
| [Trace lab](07-TRACE-LAB.md) | How to reconstruct a billing operation end to end | 45–60 min |
| [Practice](05-PRACTICE.md) exercise 5 | A credit-note/adjustment workflow, designed before it is built | 2 sessions |
| [Practice](05-PRACTICE.md) exercise 6 | A measured performance problem and a migration plan | 2–3 sessions |
| [Practice](05-PRACTICE.md) exercise 7 | A real outbox sender with leases and a failure matrix | 3+ sessions |
| [Solutions and review](06-SOLUTIONS-AND-REVIEW.md) | How to evaluate tradeoffs and acceptance criteria | 60 min |
| [Verification](VERIFICATION.md) | What was checked and what remains a limitation | 20 min |

**Be warned about exercises 5–7.** They are senior design work, and chapter 06 gives them review standards rather than answers — with one deliberate exception: exercise 5 now has a worked exemplar in [Solutions and review](06-SOLUTIONS-AND-REVIEW.md) so you can see what "a reviewable design" looks like before attempting 6 and 7 without one. If you are working alone and stall on 6 or 7 for more than a session, that is the expected outcome, not a failure; write down the questions you cannot answer and take them to someone senior. That list is itself the deliverable.

At the end, present one change with a before/after example, a transaction diagram, a regression test, and an explanation of a rejected alternative. That is the standard of reasoning this course is building toward.

## Glossary

Every term below is used in the chapters before it is defined there.

- **Workspace** — the tenant boundary. Every record carries a `WorkspaceId`, and every query filters on it. "Cross-workspace" bugs are the ones where that filter is missing.
- **ETag / `If-Match`** — an HTTP precondition. The client sends back the version it last saw; the server refuses the write if the record has moved on. Missing header → 428, malformed → 400, stale → 409.
- **Optimistic concurrency** — allowing simultaneous edits and detecting the collision at write time (via the version), rather than locking the record while someone thinks. Answers *stale intent*.
- **Idempotency key** — a caller-supplied identifier that makes a retry safe: the same key with the same request returns the original stored response instead of doing the work again. Answers *repeated intent*. Not the same problem as optimistic concurrency, and the two are not interchangeable.
- **Receipt** — the stored response for an idempotency key, kept for 24 hours here, written in the same transaction as the work so a crash cannot leave one without the other.
- **Transactional outbox** — writing "this event must be delivered" as a row in the same transaction as the business change, so the event cannot be lost if the process dies. This build marks those rows `Simulated` instead of sending them; exercise 7 is the real version.
- **Simulated / explicit processing** — there is no background worker. `POST /api/simulator/process` drains the payment queue and the outbox on demand so intermediate states are inspectable. `BillingLimits` holds the batch sizes.
- **Billing time vs. real time** — the simulator clock that drives period boundaries and invoicing, as opposed to `DateTimeOffset.UtcNow`. Confusing them is the classic billing bug.
- **Money** — an amount **and** a currency, together, with defined rounding. Never a bare `decimal`. Mixed-currency arithmetic is rejected, not coerced.
- **Anchored billing** — period boundaries fixed to the subscription's start day rather than recomputed from "now", so a late invoice run catches up instead of silently shifting every future period.
- **Archive vs. delete** — archiving hides a record from ordinary lists while preserving history and references; deletion is permitted only for records nothing points at.
- **Version** — the monotonically increasing `long` on every entity, surfaced as the ETag and checked by `CatalogService.CheckVersion`.

The existing `docs/upskill` and `docs2` material remains useful historical reading. Exact original text files are preserved under [docs/original](../docs/original). They describe an earlier in-memory, unauthenticated implementation; use this course and current source for executable instructions.
