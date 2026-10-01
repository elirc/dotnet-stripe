> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/02-stack-and-language-mastery/README.md.txt).

# 02 — Stack & Language Mastery

Learn the stack *through this repo*. Every concept points to a real file — no abstract tours. The goal
is the mental model behind each tool, anchored so it sticks.

| File | Teaches | Through |
| --- | --- | --- |
| [01-dotnet-runtime-model.md](01-dotnet-runtime-model.md) | CLR/JIT, GC, async/await, Task vs ValueTask, CancellationToken, IDisposable | the workers, the engine, `Money` |
| [02-framework-mental-models.md](02-framework-mental-models.md) | ASP.NET pipeline + DI lifetimes, model binding, options; React render model, hooks, effects | `Program.cs`, `App.tsx` |
| [03-type-system-and-contracts.md](03-type-system-and-contracts.md) | generics, nullable refs, records, pattern matching, structs vs classes, LINQ deferral; TS types | the domain, the store, `api.ts` |
| [04-tooling-and-build-system.md](04-tooling-and-build-system.md) | SDK, MSBuild, NuGet; npm, Vite, TypeScript build | the `.csproj`, `package.json`, configs |

**How to use:** when you hit a concept you're shaky on, read its section *with the anchored file open
beside it*. The repo is small enough that every concept has a concrete home.
