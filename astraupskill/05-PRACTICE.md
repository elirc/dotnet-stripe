# Practice toward independent ownership

The following exercises are proposed extensions. They are not counted as delivered features. Work in a separate checkout or copy and use a fresh database. Before implementing each exercise, write a short behavior statement, identify affected files, and list at least one failure case. After implementing it, explain the result without reading your code aloud.

## Exercise 0: break one invariant and watch the suite catch it

Do this before chapter 03. This course has a structural weakness that the other courses in the set do not: the application was **rebuilt**, not repaired, so there is no diff to run and no "before" you can simply check out. `docs/original` preserves the earlier in-memory, unauthenticated implementation, but it is a different program — `BillingStore.cs.txt` is a 330-line `Dictionary`-based store against today's 42-line SQLite-backed one. Copying it over the current file gives you a wall of compile errors, not a failing test. Do not start there.

Instead, remove **one** invariant from the current code and find out which tests defend it. Open `src/Billing.Api/Infrastructure/CatalogService.cs:151-154` and comment out the single line inside `CheckVersion`:

```csharp
public static void CheckVersion(IBillingEntity? before, long expected)
{
    // if (before is not null && before.Version != expected) throw BillingError.Conflict("This record changed. Reload and review your edit.");
}
```

That one line is the whole optimistic-concurrency story: the `If-Match`/ETag precondition that chapter 02 spends four paragraphs on. Now run the suite:

```powershell
dotnet test
```
```bash
dotnet test   # identical on macOS and Linux
```

Observed with exactly that line commented out:

```text
Failed!  - Failed: 2, Passed: 64, Skipped: 0, Total: 66 - Billing.Tests.dll (net10.0)
  HttpTests.VersionBoundaryProtectsEdits(version: "\"v99\"", status: 409)
  WorkflowTests.StaleEditPreservesWinner
```

Before you restore it, answer these:

- **Two tests out of sixty-six.** Is that reassuring or alarming? Write down what an attacker or a careless UI could now do that no test would notice. (Start with: two users open the same customer, both press Save. What does the second save now do to the first user's change, and how would either of them find out?)
- `VersionBoundaryProtectsEdits` is a `[Theory]` with four cases (`HttpTests.cs:98`): no `If-Match` → 428, `v1` → 400, `W/"v1"` → 400, `"v99"` → 409. Only the last one failed. Explain why the other three still pass with `CheckVersion` gutted — which layer rejects a missing or malformed precondition header, and why that layer is not the one you just broke. Knowing which guard owns which status code is the point.
- `StaleEditPreservesWinner` asserts on *surviving state*, not on the status code. Which assertion in it actually fired? This is the difference between "the API returned 409" and "the first writer's data is still there".

Then restore the line and confirm `Passed! - Failed: 0, Passed: 66`.

If you want a second round, pick another single guard and repeat: the currency-immutability `Conflict` in `CatalogService.Customer` (`:40-41`), or the plan-shape rules at `:51-56`. For each one, the exercise is the same — remove it, count the failures, and decide whether that count is the coverage you would want defending it in production.

## Exercise 1: explain and test an existing rule

Start with the USD 25 invoice from the [first workflow](README.md). Predict how its total changes with a 20% coupon, a USD 50 fixed coupon, and an expired coupon. Then reproduce each case in a separate subscription or workspace. Explain why the fixed discount cannot produce a negative total, and why coupon validity is evaluated at the period start for a delayed billing run.

Add a focused test for one of these cases in [WorkflowTests.cs](../tests/Billing.Tests/WorkflowTests.cs). Assert the subtotal, discount and total separately. A test that checks only the final total might miss two errors that accidentally cancel each other out. Completion means you can calculate the result by hand and identify exactly where each amount is produced.

## Exercise 2: add customer notes

Implement the 500-character notes field proposed in [the worked change](03-WORKED-CHANGE.md). Decide how old stored JSON records behave when the property is absent. Preserve version checks, immutable creation time, workspace isolation and the retry fingerprint. Show the notes in record details without inserting them as HTML.

Acceptance cases: a normal note saves and reloads; a note beyond the limit is rejected; a stale note edit preserves the winner; a repeated successful operation increments the version only once. Explain whether notes should appear in list search and whether audit history should store their contents. Do not choose either policy accidentally.

## Exercise 3: add an explicit archive filter

Customer archiving is implemented, but the list currently searches a combined text expression. Add a filter with All, Active and Archived choices. Put the filter in the HTTP query contract and apply it in SQL before pagination. Reset the page offset when the filter changes.

Create 25 customers with a mixture of archived states. Prove that the total count and page contents use the same filter. Explain why fetching one page and filtering it in React produces incorrect pagination. Include invalid filter values, an empty result, and a filter change while an earlier request is still in flight.

## Exercise 4: improve relationship selection

The existing selector searches related records and returns up to 100 matches. It can find records beyond the initial list page, but subscription labels primarily expose their identity. Improve subscription choices by returning a small server-side projection with customer name, plan name and current status.

Avoid one HTTP request per row. Keep the projection scoped to the signed-in workspace and bounded. Do not return password or signing-secret fields. Explain the tradeoff between enriching a list response and introducing a dedicated lookup endpoint. Test a related record from another workspace and a record that becomes inactive between selection and submission.

## Exercise 5: design an adjustment workflow

Invoiced usage and paid invoice amounts are immutable. Design a credit-note or adjustment workflow instead of allowing direct edits. Begin with a state diagram and a worked example: an invoice charged 150 usage units, but 20 were recorded in error. Which record expresses the correction? Does it change an existing balance or create a new financial document?

Specify authorization, currency rules, links to the original invoice, idempotency, version expectations, audit actions and transaction boundaries. Implement only after these decisions are reviewable. Tests must distinguish duplicate adjustment requests from two intentionally separate adjustments. Do not claim tax, legal or accounting compliance from this learning implementation.

## Exercise 6: evolve the storage model

Profile a workspace with thousands of usage events and subscriptions. The current application deliberately caps domain records and materializes bounded collections in some workflows.

You do not have to hunt for the expensive reads: here are two of them, and they sit in the code this course otherwise tells you to imitate. `BillingEngine.Process` (`src/Billing.Api/Infrastructure/BillingEngine.cs:102,104`) is:

```csharp
var jobs = db.All<PaymentJob>(w)
    .Where(j => j.AvailableAt <= DateTimeOffset.UtcNow)
    .OrderBy(j => j.AvailableAt).ThenBy(j => j.Id)
    .Take(BillingLimits.PaymentBatchSize).ToList();
// ...
var events = db.All<WebhookEvent>(w)
    .Where(e => e.Status == WebhookDeliveryStatus.Pending && e.AvailableAt <= DateTimeOffset.UtcNow)
    .OrderBy(e => e.AvailableAt).ThenBy(e => e.Id)
    .Take(BillingLimits.WebhookBatchSize).ToList();
```

Read `BillingSession.All<T>` in `src/Billing.Api/Infrastructure/BillingDatabase.cs:118-127` before you decide whether that is fine. `All<T>` issues `SELECT data FROM records WHERE workspace_id=$w AND kind=$k ORDER BY id LIMIT 5001`, deserializes **every** row of that kind into a `List<T>`, and throws `Conflict("Workshop record limit exceeded.")` above 5,000. So `.Where(...)`, `.OrderBy(...)` and `.Take(100)` are all LINQ-to-objects running *after* the whole table is in memory. To take the hundred oldest due jobs, the process loads up to five thousand rows, parses five thousand JSON documents, sorts them, and discards 98% of the work. The `Take` is a limit on what you *use*, not on what you *fetch*.

The 5,000-row cap is what keeps this from being a production bug, and it is disclosed. But the shape is the exact pattern the rest of the course warns you against — decide in the database, not in application code — and a learner copying `db.All<T>(w).Where(...)` into a workspace without that cap has written a table scan.

So: measure it first (time `POST /api/simulator/process` on a workspace with 4,000 pending jobs), then propose dedicated relational tables or indexed projections for the measured query. A concrete target is a `payment_jobs` table with real columns for `workspace_id`, `available_at` and `status`, an index on `(workspace_id, status, available_at)`, and a `SELECT ... WHERE ... ORDER BY available_at LIMIT 100` that returns a hundred rows from the database. Then re-measure and report both numbers. An optimization without a before number is a guess.

Write a migration plan that includes a backup, schema-version transition, validation queries and a failure path. Never silently reinterpret an unknown database file. Verify that a failed migration leaves the source database available. Explain which invariants move into database constraints and which remain application rules.

## Exercise 7: design a real outbox sender

The implemented outbox only simulates delivery. A real sender needs destination policy, timeouts, limited response bodies, retry scheduling, leases, duplicate delivery handling and observability. Write the design before connecting any network account. Use a local test receiver during implementation.

Consider the hardest case: the receiver processed the event, but the sender lost the response. A retry may deliver it again. State the receiver's deduplication contract and avoid claiming exactly-once delivery. A good submission includes a failure matrix and tests for timeout, retry exhaustion, process restart and two workers competing for one lease.

For each exercise, finish with a short review note: problem, resulting behavior, evidence, limitation and next decision. That habit turns an isolated code change into engineering work another person can assess.
