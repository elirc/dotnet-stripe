> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/03-architecture-and-patterns/README.md.txt).

# Architecture And Patterns

This section teaches boundaries, data, auth gaps, side effects, patterns, and critique through the actual repo. Start with [01-boundaries-and-layers.md](01-boundaries-and-layers.md), then read [05-pattern-catalog.md](05-pattern-catalog.md) before attempting tickets.

Verification note: the current app has one API project [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L1-L7), in-memory persistence [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17), and one React client [package.json](../../../src/Billing.Web/package.json#L1-L19).

