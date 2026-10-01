> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/02-stack-and-language-mastery/03-type-system-and-contracts.md.txt).

# Type System And Contracts

## C# Records And Immutability

Domain entities are sealed records in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123). The engine uses `with` expressions to update immutable values when rolling subscription periods and marking usage invoiced in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L88).

**Why it matters:** immutable records make value transitions explicit, which helps code review. The tradeoff is copy allocation and needing clear persistence semantics.

**Drill:** For [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L88), list old value, new value, and invariant for each `with`.

## Nullable References

The API project enables nullable refs in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L3-L5). Domain optional fields use nullable types, for example `CouponId` in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L52-L60), `AmountOff` in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L22-L30), and `PaidAt` in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L78-L92).

**Failure modes:** nullable types document possible absence, but not business permission. A nullable `CouponId` says no coupon exists; it does not say the coupon is valid for that customer. Store validation checks existence in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L106-L119).

## Structs Versus Classes

`Money` is a value type in [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57); invoices and subscriptions are reference-type records in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L52-L92). Use structs for small immutable values with value semantics, not for large mutable entities.

## Pattern Matching And Enums

Billing logic branches on pricing model in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L36-L60), and periods branch on billing cycle in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L202-L203). Enum JSON is configured as strings in [Program.cs](../../../src/Billing.Api/Program.cs#L10-L13), matching TypeScript union strings in [api.ts](../../../src/Billing.Web/src/api.ts#L3-L7).

**Failure modes:** adding an enum value requires checking C# branches, TypeScript union types, UI labels, and tests.

## TypeScript Contracts

The client mirrors API response shapes in [api.ts](../../../src/Billing.Web/src/api.ts#L9-L143). These are compile-time contracts only; runtime responses are trusted by `response.json()` in [api.ts](../../../src/Billing.Web/src/api.ts#L160-L168).

**Drill:** Add a new `InvoiceStatus` in [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31) on paper, then list every TypeScript and CSS location affected: [api.ts](../../../src/Billing.Web/src/api.ts#L5-L5), [App.tsx](../../../src/Billing.Web/src/App.tsx#L382-L383), [styles.css](../../../src/Billing.Web/src/styles.css#L198-L222).

