> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/02-stack-and-language-mastery/README.md.txt).

# Stack And Language Mastery

Learn .NET, ASP.NET Core, C#, and React through this repo’s actual seams. Start with [01-dotnet-runtime-model.md](01-dotnet-runtime-model.md), then [02-framework-mental-models.md](02-framework-mental-models.md), [03-type-system-and-contracts.md](03-type-system-and-contracts.md), and [04-tooling-and-build-system.md](04-tooling-and-build-system.md).

Verification note: the API project enables nullable references in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L3-L5), React uses strict TypeScript in [tsconfig.json](../../../src/Billing.Web/tsconfig.json#L2-L20), and all public API handlers are minimal endpoints in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L250).

