# Trace lab: reconstruct a billing operation

This lab combines source reading, browser inspection and failure reasoning. Use fictional records in a disposable workspace. Keep a small notebook with four columns: request, expected writes, observed response, and resulting state. The aim is to explain a complete operation without relying on a happy-path screenshot.

## The retry test Trace 1 asks you to read

Here is `ExactHttpRetryReturnsSameBodyAndEtag` in full, from `tests/Billing.Tests/HttpTests.cs:106-115`. The last line is the point of Trace 1: the same key with a changed payload is a 409, because the key identifies the operation, not the customer.

```csharp
    [Fact] public async Task ExactHttpRetryReturnsSameBodyAndEtag()
    {
        var session = await Register(); var key = Guid.NewGuid().ToString(); var input = new CustomerInput("Same", "same@example.test", "USD");
        var first = await Request("POST", "/api/customers", input, session.Token, key);
        var second = await Request("POST", "/api/customers", input, session.Token, key);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
        Assert.Equal(HttpStatusCode.Conflict, (await Request("POST", "/api/customers", input with { Name = "Different" }, session.Token, key)).StatusCode);
    }
```

## Trace 1: create and replay a customer

Create a customer from the UI and inspect the browser Network entry. Record the method, route, idempotency key and response ETag. Do not copy your bearer token into the notebook or a report. In [api.ts](../src/Billing.Web/src/api.ts), find where the operation key is created and where the exact serialized body is retained.

Follow the request through [Program.cs](../src/Billing.Api/Program.cs), the request parser, [BillingStore.cs](../src/Billing.Api/Infrastructure/BillingStore.cs) and the catalog. Identify the beginning and end of the transaction. Predict what an exact retry should return: the same status, entity ID, version and body. It should not create a second customer or add another business audit action.

Now read `ExactHttpRetryReturnsSameBodyAndEtag`. Explain why its changed-payload request returns 409 even though the new name would otherwise be valid. The conflict is about reusing an operation identity, not about the customer name itself.

## Trace 2: stale intent in two editors

Open the same workspace in two browser contexts and sign in to each. Load one customer in both. Save a name change in the first editor, then submit a different name from the second editor without reloading its record.

The second request should carry the old ETag and receive 409. Its draft remains visible. Use **Discard draft and reload latest** only after deciding to abandon that draft. Explain why an automatic resubmission with the newest ETag would be unsafe: the server cannot know whether the user's old intent still makes sense against the newer record.

Find both version checks: the comparison in the catalog and the predicate in the SQL update. Write a sentence describing the purpose of each. Then read `StaleEditPreservesWinner` and identify the final assertion that proves the winning value was preserved.

## Trace 3: invoice creation is more than one insert

Use a hybrid subscription and record 150 units. Before advancing time, inspect the usage event: `invoiceId` is null. Advance to the exact period end. The engine reads the plan, gathers unassigned usage inside the half-open period, calculates base and usage lines, applies the coupon, inserts the invoice, assigns usage, advances the subscription and enqueues an event.

Write down every persistent record affected. Then ask where failure could occur: an amount exceeds the supported range, the workspace reaches its record quota, or a unique invoice-period index detects duplication. All changes must roll back together. The simulator clock should also remain unchanged if the cycle fails. A receipt must not claim success for an operation that rolled back.

Run the same target time again with a new operation key. No additional invoice should appear because the subscription cursor has advanced. Compare this business-level protection with receipt replay: they address different causes of repetition.

## Trace 4: pending, declined and paid

Queue a payment and inspect both the invoice and attempt. The invoice remains Open while its version advances, and one Payment is Pending. The durable job references that exact payment ID. Try queuing another attempt before processing; it should conflict.

Process as decline. The existing attempt becomes Failed, the job is removed, and the invoice stays Open. Queue a new attempt using the invoice's latest version, then process as success. The new attempt becomes Succeeded and the invoice becomes Paid. The old failed attempt remains visible.

Read `CompletePayment`. Explain why a late failure for a succeeded attempt cannot reopen the invoice. A callback must identify both invoice and payment, so an unrelated attempt cannot be applied merely because an invoice ID exists.

## Trace 5: signed callback and durable deduplication

The owner-only `/api/webhook-lab` endpoint exposes the local workspace's generated signing secret for this exercise. Treat it as private test data. The custom signature is HMAC-SHA256 over `timestamp + "." + exact UTF-8 body`, encoded as hexadecimal. Headers are `X-Billing-Timestamp` and `X-Billing-Signature`. Both old and future timestamps beyond five minutes are rejected.

Read `SignedCallbackDeduplicatesAndRejectsConflictingEvent` in [HttpTests.cs](../tests/Billing.Tests/HttpTests.cs). It creates a pending attempt, signs a successful callback, repeats it and then changes the success result while reusing the event ID. The repeat is accepted as a duplicate; the changed event conflicts; the invoice remains Paid. No real provider is contacted.

Finish by explaining the difference between a caller's idempotency key, a provider event ID, a payment attempt ID and an invoice ID. They are four identities with different lifetimes and purposes. If your explanation treats them as interchangeable, revisit the transaction and transition code before adding another feature.
