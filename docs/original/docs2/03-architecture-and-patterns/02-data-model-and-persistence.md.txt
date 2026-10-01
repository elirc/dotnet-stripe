# 03.02 — Data Model & Persistence

## Entities and relationships

```
Customer ──< Subscription >── Plan
   │              │   │
   │              │   └──(optional) Coupon
   │              │
   │              └──< UsageEvent   (UsageEvent.InvoiceId = null ⇒ unbilled)
   │
   └──< Invoice ──< InvoiceLine
            │
            └──< Payment

WebhookEvent  (standalone outbox row; references nothing by FK)
PaymentJob    (standalone queue row; references InvoiceId)
```

`──<` means "one to many". All defined in [BillingModels.cs](../../src/Billing.Api/Domain/BillingModels.cs).
Relationships are expressed as `Guid` foreign keys on the child record (e.g.
[`Subscription.CustomerId`/`PlanId`/`CouponId`](../../src/Billing.Api/Domain/BillingModels.cs#L52-L60)),
not as object references — which is exactly how they'd map to columns in a relational DB.

## Where each entity is stored

Every entity has a dictionary keyed by `Guid` in [BillingStore](../../src/Billing.Api/Infrastructure/BillingStore.cs#L8-L17),
plus two queues:

| Collection | Holds | Anchor |
| --- | --- | --- |
| `_customers`, `_plans`, `_coupons`, `_subscriptions`, `_usageEvents`, `_invoices`, `_payments` | entity by id | [L8-L14](../../src/Billing.Api/Infrastructure/BillingStore.cs#L8-L14) |
| `_paymentJobs` (`Queue`) | pending payment jobs | [L15](../../src/Billing.Api/Infrastructure/BillingStore.cs#L15) |
| `_webhookEvents` (dict) + `_webhookQueue` (`Queue<Guid>`) | outbox rows + delivery order | [L16-L17](../../src/Billing.Api/Infrastructure/BillingStore.cs#L16-L17) |

The webhook design is the interesting one: the *entity* lives in a dictionary (so it can be updated by
id) while a separate **queue of ids** tracks delivery order — a hand-rolled durable-queue shape.

## The "indexes" that don't exist (and the queries that need them)

In-memory, every lookup-by-non-key is a linear scan. In a database these become the indexes you must
not forget. The repo's hot queries:

| Query | Today (in-memory) | Index it would need in a DB |
| --- | --- | --- |
| Unbilled usage for a period | full scan of `_usageEvents` ([GetUnbilledUsage L182-L188](../../src/Billing.Api/Infrastructure/BillingStore.cs#L182-L188)) | `(SubscriptionId, InvoiceId, OccurredAt)` |
| Customer email uniqueness | `_customers.Values.Any(…)` ([L68](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68)) | unique index on `Email` |
| Plan code uniqueness | scan ([L82](../../src/Billing.Api/Infrastructure/BillingStore.cs#L82)) | unique index on `Code` |
| Coupon code uniqueness | scan ([L96](../../src/Billing.Api/Infrastructure/BillingStore.cs#L96)) | unique index on `Code` |
| Dashboard (sort by created) | `.OrderByDescending(CreatedAt)` per call ([e.g. L26](../../src/Billing.Api/Infrastructure/BillingStore.cs#L26)) | index on `CreatedAt` per table |

> **Teaching point:** the uniqueness checks are correct *and* a future bug. As linear scans they're
> O(n) but always consistent under the lock. As a DB without a unique index they'd be a
> read-then-write race (two concurrent inserts both pass the `.Any()` check). The fix isn't app code —
> it's a unique constraint. Knowing that the *database* enforces uniqueness, not the application, is a
> mid-level insight.

## "Transaction" boundaries

There are none in the durable sense. The closest thing is the single lock. The multi-write operations
that *should* be transactional:

- [`GenerateInvoice`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L110): save invoice →
  stamp usage as billed → advance subscription period → enqueue webhook. **Four separate locked
  calls.** A crash between them leaves inconsistent state (usage billed but no invoice, or invoice
  saved but period not advanced).
- [`ApplyPaymentResult`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L169-L199): save invoice
  → save payment → enqueue webhook. Three separate calls.

In the in-memory world these are individually atomic (each `lock`) but not *collectively* atomic.
Moving to a DB makes the transaction boundary explicit and necessary (see
[Comparison 1](07-comparative-implementations.md), migration step 6).

## Immutability & "updates"

Entities are immutable records; an "update" is `entity with { … }` plus an explicit `Save*`
([e.g. mark invoice paid](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L165-L169)). The store
overwrites by key ([`SaveInvoice` L157-L160](../../src/Billing.Api/Infrastructure/BillingStore.cs#L157-L160)).
There is **no optimistic concurrency** — last write wins. In a DB this is where you'd add a
`RowVersion`/`xmin` check.

## Safe schema-change guidance (for when this becomes a real DB)

When the store becomes EF Core, treat every model change as a migration with a forward and backward
step:

1. **Additive first.** New nullable column / new table → deploy → backfill → make non-null. Never
   add a non-null column without a default in one step.
2. **Rename = add + copy + drop, across releases.** Add the new column, write both, migrate readers,
   then drop the old — don't rename in place while old code runs.
3. **Index online.** On large tables, build indexes concurrently so writes aren't blocked.
4. **The `Money` mapping is the trap.** Decide up front: two columns (`MinorUnits`, `Currency`) per
   money field, or an owned type. Changing that decision later is a data migration, not a code change.
5. **`Invoice.Lines`** ([L84](../../src/Billing.Api/Domain/BillingModels.cs#L84)) must become a child
   table or an owned collection — it can't be a column.

## Drill

Sketch the relational schema for `Invoice` + `InvoiceLine` + the `Money` fields. How many columns does
one `Money` become? How do you store `Lines`? **Self-grade — Basic:** you list the scalar columns.
**Solid:** you map each `Money` to (`minor_units`, `currency`) and `Lines` to a child table with a FK.
**Strong:** you add the missing indexes from the table above and note the unique constraints that
replace the linear-scan checks.

## Verification Notes

Entity shapes, FK fields, store collections, and query sites are read-verified on 2026-06-09. The
"needs this index" column is design guidance (inferred from the query shapes), not something the repo
declares — there is no DB.
