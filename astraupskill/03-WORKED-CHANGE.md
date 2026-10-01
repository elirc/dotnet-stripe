# Worked change: safe customer editing

The original dashboard could create customers but could not edit or delete them. A mid-level implementation needs more than a PUT endpoint. It must explain how the edit interacts with identity, uniqueness, references, concurrent requests and the user’s unsaved draft. This guide walks through the implemented change, then asks you to extend it in a small, testable way.

## Define the behavior first

An unused customer can change name, email, currency and archived state. Once another record references that customer, its currency cannot change. Otherwise a USD subscription could suddenly point at a EUR customer while its plan and historical invoices remain USD. The customer can still be renamed or archived. Deleting a referenced customer returns a conflict; the user should archive it instead.

The update requires the version the editor originally loaded. A successful update increments that version exactly once. A repeated request with the same idempotency key must not increment it again. A stale request must not overwrite the winner. These sentences are acceptance criteria, not implementation details.

## Start at the request contract

The active contract is `CustomerInput` in [CatalogRequests.cs](../src/Billing.Api/Domain/CatalogRequests.cs):

```csharp
public sealed record CustomerInput(
    string Name,
    string Email,
    string Currency,
    bool IsArchived = false);
```

There is no `WorkspaceId`, `CreatedAt` or `Version` field for a caller to rewrite. The server derives workspace identity from the session, preserves the creation timestamp, and obtains the expected version from If-Match. Unknown JSON properties are rejected. That prevents a form from quietly sending fields the API never intended to accept.

## Implement the domain rule

In [CatalogService.cs](../src/Billing.Api/Infrastructure/CatalogService.cs), `Customer` loads the current record with both workspace and ID. It compares the expected version, normalizes email and currency, and checks whether a currency change would affect a referenced customer. It creates a new immutable record using the old `CreatedAt` and `Version + 1`.

The database update is conceptually:

```sql
UPDATE records SET data = $data
WHERE workspace_id = $workspace
  AND kind = 'customers'
  AND id = $id
  AND version = $expected;
```

The real helper uses parameters for every value. If zero rows are updated, the service returns a conflict. Do not remove the version predicate because the service already compared versions: the predicate is the persistence boundary and makes the intended invariant visible in SQL.

## Wrap the mutation in a durable operation

[BillingStore.cs](../src/Billing.Api/Infrastructure/BillingStore.cs) owns the transaction. The endpoint passes it a callback that performs the catalog change. The store checks current authorization, looks for a matching receipt, executes the callback once, writes audit history, stores the serialized response, and commits.

For example, the first request might be:

```http
PUT /api/customers/11111111-1111-1111-1111-111111111111
Authorization: Bearer <your in-memory session token>
Content-Type: application/json
Idempotency-Key: customer-edit-demo-001
If-Match: "v1"

{"name":"Ada Studio Ltd","email":"ada@example.test","currency":"USD","isArchived":false}
```

The UUID is illustrative; substitute your actual record ID. A success returns the updated entity and ETag `"v2"`. An exact retry returns that same response. Changing the name again requires a new operation key and the current ETag. The retained receipt describes the first edit, not a mutable draft.

## Keep the browser honest

[forms.ts](../src/Billing.Web/src/forms.ts) defines the customer controls. [App.tsx](../src/Billing.Web/src/App.tsx) holds a separate editable copy of the selected record. Saving creates an `Operation` object containing method, path, exact serialized body, key and version. [api.ts](../src/Billing.Web/src/api.ts) sends those values.

On a definitive validation or conflict response, the browser retains the form values and displays the server message. On a transport error or server failure, it also retains the operation and offers **Retry exact operation**. It disables other mutations until that uncertainty is resolved. After a successful save, failure to reload the list is described as a refresh problem, not a failed save.

## Verify the change at two levels

`StaleEditPreservesWinner` proves the domain/persistence rule. `RegisterCrudAndEtagRoundtrip` proves that routing, request parsing and response headers expose it correctly. `ExactHttpRetryReturnsSameBodyAndEtag` proves the retry contract through HTTP. Deletion tests prove both the unused-record success and the referenced-record conflict.

Now propose a small extension: a customer `Notes` field limited to 500 characters. Before coding, decide whether blank means empty text or null, whether it appears in list search, and whether the audit should contain the text. Prefer an audit action without sensitive note contents. Add the field to the immutable model, request contract, form and validation; consider how existing JSON records deserialize when the field is absent. Write a test that edits notes without changing currency or creation time. This is a proposed exercise, not an already implemented feature.
