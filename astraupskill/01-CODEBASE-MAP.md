# Follow one request through the codebase

Start with a concrete question: when you rename a customer, which files decide whether that name is accepted, whether the record belongs to your workspace, and whether another edit has already won? Do not begin by reading every file alphabetically. Trace one operation, then widen the map.

## The active layers

| File | Responsibility | What should stay elsewhere |
|---|---|---|
| [App.tsx](../src/Billing.Web/src/App.tsx) | Screens, selection, draft state, loading, errors and retries | Authoritative permission checks |
| [forms.ts](../src/Billing.Web/src/forms.ts) | Field definitions, defaults and form-to-request conversion | Database validation |
| [api.ts](../src/Billing.Web/src/api.ts) | HTTP calls, bearer header, operation identity and typed errors | Business pricing rules |
| [Program.cs](../src/Billing.Api/Program.cs) | Routes, bounded request handling, local-origin checks and error responses | Invoice arithmetic |
| [IdempotencyMiddleware.cs](../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs) | Parse keys, ETags, body bytes and fingerprints | Durable response storage |
| [WorkspaceAuth.cs](../src/Billing.Api/Infrastructure/WorkspaceAuth.cs) | Password verification, sessions and current memberships | Customer CRUD |
| [BillingStore.cs](../src/Billing.Api/Infrastructure/BillingStore.cs) | Transactional authorization, receipt replay, mutation and audit | Form rendering |
| [CatalogService.cs](../src/Billing.Api/Infrastructure/CatalogService.cs) | CRUD invariants and SQL-backed list pages | HTTP response formatting |
| [BillingEngine.cs](../src/Billing.Api/Infrastructure/BillingEngine.cs) | Period invoicing, payment transitions and simulated processing | Real payment-provider calls |
| [BillingDatabase.cs](../src/Billing.Api/Infrastructure/BillingDatabase.cs) | Schema, parameterized SQL, version predicates and relationships | User-interface decisions |
| [BillingModels.cs](../src/Billing.Api/Domain/BillingModels.cs) | Immutable domain records | Persistence connections |
| [Money.cs](../src/Billing.Api/Domain/Money.cs) | Currency-safe arithmetic and integer minor units | Currency conversion rates |

`Contracts/Requests.cs`, `Contracts/Responses.cs` and the old clock abstraction remain compatibility/reference types from the original project. The active request records are in `Domain/CatalogRequests.cs` and `Infrastructure/WorkspaceAuth.cs`. The old background-worker files now document explicit simulator batch limits; no hosted worker runs automatically. Read the current registrations in `Program.cs` to distinguish a type that exists from a component actually used at runtime.

## Customer edit trace

The list page loads a bounded SQL page and displays each record's version. Clicking Edit copies that record into form state. A later list refresh does not silently replace your draft. On Save, the browser serializes the request once and creates one idempotency key. It sends `PUT /api/customers/{id}` with `If-Match: "v1"`.

The API reads at most 32 KiB and validates the JSON contract. The request parser requires a strong, single ETag and creates a fingerprint from method, path, query, If-Match and exact body. `BillingStore.Write` opens an immediate SQLite transaction. It resolves the bearer token against current session and membership rows, so a role change applies even to an already signed-in user.

If a matching receipt exists, the stored status, body and ETag are returned without rerunning the edit. Otherwise the catalog loads the customer using the actor's workspace and the route ID. It checks the expected version and business rules, then writes a replacement immutable record whose version is one greater. SQL also includes the old version in its `WHERE` clause. The audit entry and response receipt are inserted before the transaction commits.

The browser clears the operation only after a successful response. If the subsequent list refresh fails, it says the save succeeded and asks for Refresh. This distinction prevents an ordinary refresh failure from becoming a duplicate mutation.

## Persistence topology

The database uses a deliberate hybrid design. The `records` table stores `(workspace_id, kind, id)` as a relational primary key and the immutable domain record as JSON. A generated version column and partial unique indexes enforce selected invariants. The `links` table stores typed relationships and uses composite foreign keys that include workspace identity. A subscription therefore cannot link to a customer in another workspace, even if a caller supplies that customer's UUID.

This is not a fully normalized financial ledger. Many domain checks remain in application code, and internal workflows read bounded collections. The workshop caps a workspace at 5,000 domain records. A scaling exercise should profile those reads and consider dedicated relational tables before claiming production readiness.

## Useful reading exercise

Find `QueuePayment`, then list every row it changes: payment, payment job, relationship rows and invoice version. Next find `CompletePayment`. Explain why it updates the existing attempt rather than inserting a second payment with a new identity. Finally locate the transaction owner. If you cannot point to where rollback happens, you have not yet established that the workflow is atomic.

Use [WorkflowTests.cs](../tests/Billing.Tests/WorkflowTests.cs) and [HttpTests.cs](../tests/Billing.Tests/HttpTests.cs) to check your map against executable behavior. The former tests domain and persistence boundaries; the latter adds routing, headers, JSON and response status.
