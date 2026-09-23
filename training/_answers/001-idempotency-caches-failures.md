# Sealed answer — Incident 001

**BUILD: VERIFIED** (`dotnet build src/Billing.Api` succeeds on branch
`training/incidents/001-idempotency-caches-failures`, commit `8b0c838`, after also
applying the two pre-committed build fixes from `training/_answers/review.md` finding 0
— those fixes are bundled in this commit, unrelated to the planted bug itself.)

## Root cause

`src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs`, the response-caching block at
the end of `InvokeAsync`, changed from:

```csharp
if (context.Response.StatusCode is >= 200 and < 300)
{
    var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.IdempotencyTtlHours));
    store.Set(cacheKey, requestHash, context.Response.StatusCode, context.Response.ContentType ?? "application/json", responseBody, ttl);
}
```

to (the incident diff):

```csharp
// Cache every response we saw, not just successes, so a client that retries
// with the same Idempotency-Key never re-runs a side effect twice.
var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.IdempotencyTtlHours));
store.Set(cacheKey, requestHash, context.Response.StatusCode, context.Response.ContentType ?? "application/json", responseBody, ttl);
```

The comment's premise is backwards: caching *failures* under the client's
`Idempotency-Key` does not stop a duplicate side effect — it stops the client from ever
successfully retrying at all, for up to `IdempotencyTtlHours` (default 24h,
`BillingOptions.cs:8`). The first time a client hits `POST /api/invoices/{id}/pay` with a
given key and gets a 400 (here: "Invoice is already paid" — a transient truth at the
moment of the call, not a permanent one), that exact response is now pinned under that
key regardless of what actually happens to the invoice afterward. The frontend derives
its `Idempotency-Key` from the invoice id (so every retry on the same invoice reuses the
same key — that's intentional, correct client behavior for avoiding double-charges), so
every subsequent click replays the stale error, even after the invoice's real state
changes.

## Diagnostic path a senior would take

1. Dashboard says `Status: Open`; the API insists "already paid" — that contradiction
   is the first clue this is a caching/replay bug, not a business-logic bug, because the
   400 body and the live invoice state have diverged.
2. `Idempotency-Replayed: true` is in the response headers (`IdempotencyMiddleware.cs:46`)
   — that header is the whole story once you notice it in the raw HTTP response, not just
   the parsed JSON body ops usually looks at.
3. Confirm by hand: `curl` the pay endpoint with a fresh `Idempotency-Key` for the same
   invoice — it succeeds. The bug isn't in `BillingEngine`/`BillingStore` at all.
4. `git log -p -- src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs` (or `git
   blame`) finds the one-line change: the `if (... is >= 200 and < 300)` guard is gone.

## Fix

Restore the status-code guard — only cache 2xx responses:

```csharp
if (context.Response.StatusCode is >= 200 and < 300)
{
    var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.IdempotencyTtlHours));
    store.Set(cacheKey, requestHash, context.Response.StatusCode, context.Response.ContentType ?? "application/json", responseBody, ttl);
}
```

This is a legitimate design tradeoff, not a cop-out: not caching failures means a client
*can* accidentally re-run a side effect if the original request actually succeeded but
the response was lost in transit — but that's a much rarer failure mode than "every
failed request becomes permanently unretriable," and it's the one every major payments
API (Stripe included) actually ships: only successful responses are idempotency-cached.

## Regression test

A unit test against `IdempotencyMiddleware` (there is no test project yet — this is also
the natural first test to write for `training/review/characterization-tests-billingengine.md`'s
sibling gap in `IdempotencyMiddleware`/`IdempotencyStore`):

1. Send `POST /api/invoices/{id}/pay` with `Idempotency-Key: K` against an invoice that
   is already `Paid` → assert `400`.
2. Mutate the invoice back to `Open` (simulate the real webhook path, or call
   `BillingStore.SaveInvoice` directly in the test).
3. Send the *same* request again with the *same* key `K` → assert the response is now
   whatever the live handler would produce for an `Open` invoice (`202`), **not** a
   replay of the cached `400`.
4. Separately: send a request that succeeds (`2xx`), retry with the same key + same body
   → assert the cached response *is* replayed (the existing correct behavior — the fix
   must not remove idempotency for success paths).

## Postmortem (model)

A one-line change to `IdempotencyMiddleware`'s caching condition — removing the `2xx`
guard — was merged with a comment claiming it prevented duplicate side effects on retry.
It does the opposite for any request that fails: the failure response is now pinned to
the client's idempotency key for up to 24 hours, so a legitimately retryable operation
(pay an invoice whose state has since changed) becomes permanently stuck from the
client's point of view, even though the server-side state is correct. Because there is no
test project in this repository (`training/_answers/review.md` finding 17), nothing
exercised the idempotency middleware's failure path before this shipped, and the bug was
only visible in production as a slow trickle of "my payment is stuck" tickets that looked
like a billing-logic bug rather than a caching bug — the fix that would have caught this
is exactly the regression test above: a test that changes the underlying state between
two retries with the same idempotency key and asserts the second retry reflects the new
state when the first response wasn't 2xx.
