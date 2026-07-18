# Fake Code Contrasts

Each fake snippet is illustrative only and not from this repo.

## 1. Money As Double

```csharp
// Illustrative fake code: not from this repo.
var total = 29.99 * quantity;
```

Better here: use `Money` from [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57), as invoice totals do in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L64).

## 2. Entity Exposed Everywhere

```csharp
// Illustrative fake code: not from this repo.
return Results.Ok(invoice);
```

Better here: `InvoiceResponse.From` maps invoice details in [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L38-L71). Remaining raw dashboard domain exposure in [Program.cs](../../../src/Billing.Api/Program.cs#L53-L62) is a refactor candidate.

## 3. Missing Permission Filter

```csharp
// Illustrative fake code: not from this repo.
api.MapPost("/invoices/{id}/pay", id => Pay(id));
```

Current repo has no auth around [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221); better production shape checks tenant ownership before calling [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L154).

## 4. Side Effect Before State Save

```csharp
// Illustrative fake code: not from this repo.
await SendWebhook(invoice);
SaveInvoice(invoice);
```

Current engine saves invoice and enqueues webhook state in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105). Production better is transactional outbox.

## 5. Swallowed Worker Exceptions

```csharp
// Illustrative fake code: not from this repo.
catch { }
```

Current webhook worker logs failures and stores error/backoff in [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L40-L55). Better production also emits metrics.

## 6. Stale React Closure

```tsx
// Illustrative fake code: not from this repo.
useEffect(() => { refresh(filter); }, []);
```

Current `useMemo` dependencies include `dashboard` in [App.tsx](../../../src/Billing.Web/src/App.tsx#L49-L51). Better habit: dependencies tell React what values the calculation reads.

## 7. Ad Hoc Fetch Per Button

```ts
// Illustrative fake code: not from this repo.
fetch("/api/customers", { method: "POST", body: JSON.stringify(form) });
```

Current writes go through `postJson`, adding JSON headers and idempotency keys in [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158).

## 8. Dynamic Contract

```ts
// Illustrative fake code: not from this repo.
const invoice: any = await response.json();
```

Current client defines `Invoice`, `DashboardResponse`, and related types in [api.ts](../../../src/Billing.Web/src/api.ts#L82-L143). Runtime validation is still a possible improvement because `response.json()` is trusted in [api.ts](../../../src/Billing.Web/src/api.ts#L160-L168).

## 9. Unbounded Database Query

```csharp
// Illustrative fake code: not from this repo.
return db.Invoices.ToList();
```

Current in-memory dashboard returns all invoices in [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63). If migrated to EF, add paging/projection.

## 10. Retry Without Idempotency

```csharp
// Illustrative fake code: not from this repo.
for (...) await CreateInvoice();
```

Current HTTP writes can replay via [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L88), but internal invoice cycle retries still need durable idempotency around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124).

