# Test claims, then debug the boundary that failed

Run [check.ps1](../scripts/check.ps1) from the project root. It runs the .NET tests, installs the locked frontend dependencies, checks TypeScript and builds the browser bundle. Tests create fresh, randomly named temporary SQLite databases. They never need your normal workshop database or an external provider account.

```powershell
dotnet test tests/Billing.Tests/Billing.Tests.csproj --nologo
dotnet test tests/Billing.Tests/Billing.Tests.csproj --filter FullyQualifiedName~StaleEditPreservesWinner
dotnet test tests/Billing.Tests/Billing.Tests.csproj --filter FullyQualifiedName~SignedCallback
```

Use the full suite after a domain or persistence change. Use a focused test while diagnosing one failure. Repeating a green suite without any intervening change does not create stronger evidence; add a test for a genuinely unproven behavior instead.

## What each test layer proves

[MoneyTests.cs](../tests/Billing.Tests/MoneyTests.cs) checks arithmetic and serialization: exact minor units, supported currencies, fractional-cent rejection, range bounds and rounding. These tests do not prove that an endpoint returns the correct status code. They isolate a small value object so a failure points directly at its contract.

[WorkflowTests.cs](../tests/Billing.Tests/WorkflowTests.cs) uses the actual SQLite schema and service methods. It verifies restart persistence, rollback, foreign keys, unique indexes, stale versions, usage locking, catch-up billing, cancellation, payment transitions and durable receipts. The concurrent-key test sends four tasks through independent database connections and requires one resulting customer. It is more useful than mocking a repository and asserting that `Save` was called once.

[HttpTests.cs](../tests/Billing.Tests/HttpTests.cs) starts the real ASP.NET application inside a test host with an injected temporary database. It tests registration, CRUD routes, ETags, strict JSON, body limits, origin/host boundaries, pagination, tenant isolation, logout and signed callback deduplication. This layer catches mistakes between a correct service and an incorrectly wired endpoint.

Browser validation adds the real React controls, fetch calls, displayed errors and responsive layout. A successful API test alone cannot establish that a button uses the selected record's version or that an unsaved draft survives a conflict. Conversely, a screenshot cannot prove transaction rollback.

## Read a failure precisely

Suppose saving an edit returns 409. Inspect the Network entry before changing code. Check the route ID, If-Match value and response message. A uniqueness conflict and a stale-version conflict share a status family but imply different corrections. Then inspect the stored record through GET. If it is already version 2 and your form sent version 1, the server is protecting another edit correctly.

For a 400 response, inspect the body structure and JSON types. `"quantity":"150"` is a string, not a number. Enum values are named strings such as `"Hybrid"`, not integer ordinals. Duplicate properties and unknown fields are rejected. Timestamps need `Z` or an explicit offset; a timezone-free timestamp is ambiguous and should not be repaired by guessing on the server.

For 422, look for a domain rule: a negative price, unsupported currency, future usage, wrong metric, or impossible coupon terms. For 428, add the current ETag. For 503, preserve the original operation key and retry after the database becomes available. Do not generate a fresh create request merely because the first response was lost.

## A rollback investigation

The workflow test `FailureRollsBackAllWrites` inserts a customer and deliberately throws before commit. Afterward, the database must contain no customer. `ReceiptAndAuditRollBackWithMutationFailure` repeats that idea through the operation store and also checks receipts and audit history. A partially successful business operation is a correctness failure even if each individual SQL statement is valid.

When adding a new workflow, place a deliberate failure after its first write in a test. Assert all relevant tables afterward, including queues and relationship rows. Checking only the primary entity can miss an orphaned job or misleading audit event.

## Debug the packaged application too

Development uses Vite's same-origin API proxy. The local package serves the compiled React bundle from ASP.NET. These are different asset paths. Build with [publish-local.ps1](../scripts/publish-local.ps1), start the DLL from its output directory and open the API port. Verify that the page loads, its asset requests succeed, and `/api/unknown` returns a JSON 404 rather than the single-page HTML shell.

The database file contains fictional customer data, password derivations, session hashes and workspace signing secrets. Do not attach it to a bug report. Use a minimal reproduction, the response trace ID, a sanitized request body and the failing test. [VERIFICATION.md](VERIFICATION.md) records the actual delivery evidence and limits separately from this reusable debugging procedure.
