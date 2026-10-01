# Concurrency, receipts and the transaction boundary

This is the second half of chapter 02. Read [02-CONCEPTS.md](02-CONCEPTS.md) first: it covers money, tenancy and time, which are individually hard but independently learnable. The three ideas here are harder because they are all answers to the same question — *what happens when a request arrives that the server did not expect in the order it arrived* — and juniors routinely reach for the wrong one.

Fix the distinction before anything else:

| Problem | Symptom | Mechanism | Status code |
|---|---|---|---|
| **Stale intent** — two people editing, one of them looking at an old version | The second save silently destroys the first | Optimistic concurrency: version / ETag / `If-Match` | 409 (428 if the precondition is missing, 400 if malformed) |
| **Repeated intent** — one person, one intention, sent twice because the network ate the answer | The same charge happens twice | Idempotency key + stored receipt | 200 with the *original* response (409 if the key is reused with different content) |
| **Partial intent** — one operation that must change five things | Three of the five landed | Transaction | 503 on lock contention; retry the exact operation |

They compose: a single `PUT /api/customers/{id}` can carry an `If-Match` header *and* an `Idempotency-Key` header, and its work happens inside one transaction. None of the three substitutes for either of the others.

## Optimistic concurrency is about stale intent

Suppose two tabs load customer version 1. Tab A changes the name and commits version 2. Tab B still submits `If-Match: "v1"`. Returning 409 protects A's update and makes B review the newer state. A missing precondition returns 428; a malformed ETag returns 400. The browser preserves B's draft and provides an explicitly destructive **Discard draft and reload latest** action.

The service checks the version for a helpful error, and SQL includes it in the update predicate for persistence protection. Both matter. A client-side disabled button alone cannot protect against another tab or a direct HTTP caller.

### Worked failure: the lost update

Watch what the missing check costs. Chapter 05's Exercise 0 has you comment out the one line inside `CatalogService.CheckVersion` (`src/Billing.Api/Infrastructure/CatalogService.cs:151-154`). Here is the sequence that then goes wrong, with no error anywhere:

| # | Actor | Request | Server state after |
|---|---|---|---|
| 1 | Tab A | `GET /api/customers/{id}` → `200`, `ETag: "v1"`, body `{ "name": "Ada", "email": "ada@example.test" }` | `Ada`, version 1 |
| 2 | Tab B | `GET /api/customers/{id}` → `200`, `ETag: "v1"`, same body | `Ada`, version 1 |
| 3 | Tab A | `PUT /api/customers/{id}`, `If-Match: "v1"`, body `{ "name": "Ada Studio", ... }` → `200`, `ETag: "v2"` | `Ada Studio`, version 2 |
| 4 | Tab B | `PUT /api/customers/{id}`, `If-Match: "v1"`, body `{ "name": "Ada", "email": "billing@example.test" }` | **with the check:** `409`, state unchanged, B is told to reload |
| 4′ | Tab B | the same request, **with the check removed** | `200`. The name reverts to `Ada` and A's edit is gone. Nobody is told anything |

Step 4′ is the whole reason the mechanism exists. Note what it is *not*: no exception was swallowed, no data was corrupted at the storage layer, and every individual request succeeded. The damage is that one user's work disappeared, and the only evidence is in the audit history — which is exactly why this application writes one.

Two tests in `tests/Billing.Tests` defend that line, and only two: `HttpTests.VersionBoundaryProtectsEdits` (the `"v99"` case) and `WorkflowTests.StaleEditPreservesWinner`. The second is the more valuable one, because it asserts on the *surviving record* rather than on the status code. A test that checks only "the API returned 409" would still pass against an implementation that returned 409 and wrote the row anyway.

### Why the version is checked twice

`CheckVersion` throws a friendly `Conflict` *before* the write is attempted, and the SQL `UPDATE` carries the version in its `WHERE` clause. That looks redundant and is not:

- The service-level check exists to produce a good error message and to avoid doing pointless work.
- The SQL predicate exists because the service-level check is a read followed by a write, and something can change in between. The database is the only place where "check the version and write" is one atomic act.

If you ever have to drop one, drop the friendly one. The predicate is the correctness mechanism; the exception is the user experience.

## Idempotency is about repeated intent

A timeout does not prove that a write failed. The server might have committed just before the response was lost. An idempotency key names one exact operation. Its receipt is stored in the same transaction as the mutation and audit record. Retrying the same request returns the stored response. Reusing the key with different content returns 409.

The fingerprint includes If-Match. Changing only the precondition while keeping the same key is still a different operation. Receipts are scoped to workspace and user and retained for 24 hours. Authorization is checked before replay, so an expired session cannot use an old receipt as an authorization bypass. The browser's pending request survives a network error only while the tab remains open; it is not durable browser storage.

### Worked failure: the response that died in transit

The scenario `IdempotencyMiddleware` exists for, step by step. A user clicks **Queue payment** once; their Wi-Fi drops as the answer comes back.

| # | Actor | What happens | Durable state |
|---|---|---|---|
| 1 | Browser | `POST /api/invoices/{id}/payments`, `Idempotency-Key: 7f3c...`, body `{ "amount": 2500, "currency": "USD" }` | — |
| 2 | Server | Opens a transaction. Creates the payment attempt, queues the payment job, writes the audit row, and writes the **receipt** (key → response body + status + ETag) — all in that one transaction. Commits | attempt exists, receipt exists |
| 3 | Network | The `201` response never arrives. The browser sees a timeout | attempt exists, receipt exists |
| 4 | Browser | Retries **the same request with the same key** | — |
| 5 | Server | Authorizes the caller *first*, finds the receipt, compares the request fingerprint, and returns the **stored** `201` body and ETag. No second attempt is created | still exactly one attempt |

Now the variations that distinguish a real implementation from a hopeful one:

- **Same key, different body** (the user edited the amount and resubmitted) → `409`. The key names one operation; reusing it for a different one is a client bug, and silently doing the new work would be worse than refusing.
- **Different key, same body** (the user genuinely wants to pay twice) → two attempts. Correct. Idempotency is about the *caller's* intent, and a new key is the caller saying "this is a new intention".
- **Same key, expired session** → the authorization check runs before the replay, so the answer is `401`, not a replayed `201`. A receipt is not a capability.
- **Crash between step 2's work and step 2's receipt** — impossible, and that is the point of writing them in one transaction. If the receipt could be written separately, a crash in the gap would leave a charge with no receipt (so the retry charges again) or a receipt with no charge (so the retry returns success for work that never happened).

That last bullet is the connection to the next section: idempotency is only as good as the transaction it is written inside.

## Transactions define the unit of correctness

Creating an invoice also assigns usage, advances a subscription, and enqueues an event. Those writes form one business operation. If any part fails, the transaction rolls everything back, including the receipt. SQLite permits only one writer at a time, so this workshop uses an immediate transaction and a five-second busy timeout. A busy response is 503; retry the exact operation.

The [SQLite transaction documentation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions) explains the write-lock tradeoff. The [generated-column documentation](https://www.sqlite.org/gencol.html) explains why generated values can be indexed but are not used as this schema's primary key. Read those constraints before redesigning the persistence layer.

### What one invoice run actually writes

Follow `BillingEngine` through a period close. Every row below is inside **one** transaction:

| # | Write | Where | Why it cannot be separated |
|---|---|---|---|
| 1 | Insert the `Invoice` with its snapshotted lines, and link it to the subscription and the customer | `BillingEngine.cs:40` | The lines are a snapshot precisely so a later plan rename cannot rewrite them |
| 2 | Stamp every `UsageEvent` of that period with the invoice id | `BillingEngine.cs:41-46` | If this fails after the invoice exists, the same usage bills again next period |
| 3 | Advance the `Subscription` to the next anchored window | `BillingEngine.cs:47-53` | If this fails after the invoice exists, the next run re-invoices the same period |
| 4 | Insert the outbox `WebhookEvent` (`invoice.created`) | `BillingEngine.cs:54`, via `EnqueueEvent` | A committed invoice with no event means a downstream system never learns about it; an event with no invoice means it learns about something that does not exist |
| 5 | Insert the audit row | `BillingStore.cs:35` | The audit trail must not describe writes that were rolled back |
| 6 | Insert the idempotency receipt, if a key was supplied | `BillingStore.cs:36-38` | See the previous section |

Steps 5 and 6 are worth opening: `BillingStore.Mutate` looks up the receipt, runs the change, writes the audit row and writes the receipt, all inside the one session it was handed. That is the code that makes the last bullet of the previous section true.

Six writes, one commit. Pull any one of them out of the transaction and you can name the bug it causes — that exercise, done out loud, is the fastest way to understand why "just save each thing as you go" is not an option in billing code.

Two honest limitations to carry forward. First, SQLite's single-writer model means contention shows up as `SQLITE_BUSY` rather than as a deadlock; this application surfaces it as `503` and asks you to retry the exact operation, which is safe *because* the operation is idempotent. Second, the outbox row in step 4 is marked `Simulated` rather than delivered — nothing in this build sends it. Making that real, with leases, bounded retries and uncertain outcomes, is Exercise 7, and the reason it is the last exercise is that it needs everything in this chapter at once.
