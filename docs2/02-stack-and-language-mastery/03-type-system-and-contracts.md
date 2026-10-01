> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/02-stack-and-language-mastery/03-type-system-and-contracts.md.txt).

# 02.03 — Type System & Contracts

The C# type system carries a lot of this design's correctness. Each feature, anchored.

## Records

`record` gives value equality + concise immutable types. The entire domain is records
([BillingModels.cs](../../src/Billing.Api/Domain/BillingModels.cs)) and so are the DTOs
([Requests.cs](../../src/Billing.Api/Contracts/Requests.cs), [Responses.cs](../../src/Billing.Api/Contracts/Responses.cs)).
Two flavors:

- **`sealed record` (reference type):** entities like [`Invoice`](../../src/Billing.Api/Domain/BillingModels.cs#L78).
  Value equality, but heap-allocated; "update" via `with`.
- **`readonly record struct` (value type):** [`Money`](../../src/Billing.Api/Domain/Money.cs#L5).
  Value semantics + no heap allocation.

`with` expressions produce a modified copy: [`invoice with { Status = InvoiceStatus.Paid, PaidAt = now }`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L166).
Positional records also give you deconstruction and a primary constructor for free.

## Structs vs classes (the `Money` decision)

| | `class` (record) | `struct` (`Money`) |
| --- | --- | --- |
| Storage | heap | stack / inline |
| Copy | reference | by value |
| Allocation | per `new`/`with` | none |
| Good for | larger, identity-bearing entities | small, immutable values used in hot loops |

`Money` is a struct because it's tiny (two fields) and created constantly; an `Invoice` is a class
because it's larger and passed by reference. Getting this split right is a real design skill — see
[02/01 GC](01-dotnet-runtime-model.md).

## Nullable reference types

`<Nullable>enable</Nullable>` ([csproj L4](../../src/Billing.Api/Billing.Api.csproj#L4)) makes reference
types non-null by default; `?` marks the nullable ones. The domain uses this precisely:

- [`Coupon.AmountOff` is `Money?`](../../src/Billing.Api/Domain/BillingModels.cs#L27) and
  `ExpiresAt` is `DateTimeOffset?` — *optional by design*.
- [`Subscription.CouponId` is `Guid?`](../../src/Billing.Api/Domain/BillingModels.cs#L55) — a subscription
  may have no coupon.
- The store's `Try` methods use the **null-forgiving operator** `out plan!`
  ([L139](../../src/Billing.Api/Infrastructure/BillingStore.cs#L139)) to satisfy the compiler on the
  `Dictionary.TryGetValue` out-param idiom — a deliberate "I know this is fine" annotation.

**Lesson:** nullability is documentation enforced by the compiler — `Money?` vs `Money` tells you
whether absence is meaningful.

## Pattern matching

Used heavily and idiomatically:

- **Type + relational patterns** in exception filters: [`catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)`](../../src/Billing.Api/Program.cs#L79) — catch *only* expected domain errors, let the rest bubble.
- **Property/relational patterns** in validation: [`request.PercentOff is < 0 or > 100`](../../src/Billing.Api/Program.cs#L136).
- **`is` declaration patterns:** [`subscription.CouponId is Guid couponId`](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L28) binds the non-null value.
- **List/range pattern:** the `[..8]` slice in [InvoiceNumber](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L206).

The `catch … when` filter is the one to internalize — it's how the engine/HTTP boundary stays clean
(domain exception → 400) without swallowing the unexpected.

## Generics

- [`IReadOnlyList<T>`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L24) everywhere returns —
  exposes read-only collections so callers can't mutate the store's snapshots.
- [`IdempotencyReplayResponse<T>`](../../src/Billing.Api/Contracts/Responses.cs#L76) — a generic wrapper
  (defined though not currently used; note it).
- [`EnsureExists<T>`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L323-L329) — one generic guard
  reused for customers/plans/coupons/subscriptions.

## LINQ & deferred execution

LINQ queries are lazy — they don't run until enumerated. The store **forces** evaluation with
`.ToArray()` *inside the lock* ([L26](../../src/Billing.Api/Infrastructure/BillingStore.cs#L26),
[GetUnbilledUsage L188](../../src/Billing.Api/Infrastructure/BillingStore.cs#L188)) — crucial, because
returning a lazy `IEnumerable` that's enumerated *after* the lock releases would read the dictionary
without synchronization (a concurrency bug). **This is a subtle, correct decision:** materialize before
you leave the lock.

**`IQueryable` vs `IEnumerable`:** there's no `IQueryable` here (no EF). After S-1, the distinction
becomes load-bearing — keeping a query `IQueryable` lets it translate to SQL; calling `.ToList()` too
early pulls the whole table into memory and filters in C# (the in-memory behavior, but now a perf bug).

## The cross-language contract (C# ↔ TypeScript)

The wire contract is mirrored **by hand**: C# DTOs ([Responses.cs](../../src/Billing.Api/Contracts/Responses.cs))
↔ TS interfaces ([api.ts L16-L143](../../src/Billing.Web/src/api.ts#L16-L143)). Casing flips
(PascalCase → camelCase) via ASP.NET's web JSON defaults; enums are strings on both sides. **The risk:**
nothing enforces the mirror — rename a C# field and the TS type silently diverges (the boundary leak in
[03/01](../03-architecture-and-patterns/01-boundaries-and-layers.md)). TS itself is `strict`
([tsconfig L10](../../src/Billing.Web/tsconfig.json#L10)), so the *client* is internally type-safe; the
*contract* is not.

## Drill

Find every nullable (`?`) in the domain and explain why absence is meaningful for each. **Self-grade —
Basic:** you list `CouponId`, `AmountOff`, `PaidAt`. **Solid:** you explain each (`PaidAt == null` ⇒
unpaid). **Strong:** you note where a nullable is *load-bearing logic* — `UsageEvent.InvoiceId == null`
is the unbilled flag ([store L184](../../src/Billing.Api/Infrastructure/BillingStore.cs#L184)), so a
nullable column drives the whole billing-selection query.

## Verification Notes

Every language-feature anchor read-verified on 2026-06-09. The deferred-execution-under-lock reasoning
is a read-verified observation about why `.ToArray()` is inside the lock; not executed.
