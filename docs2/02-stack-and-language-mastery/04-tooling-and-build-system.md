> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/02-stack-and-language-mastery/04-tooling-and-build-system.md.txt).

# 02.04 — Tooling & Build System

How source becomes a running program, both sides.

## .NET side

### The SDK & MSBuild

`dotnet` is a thin driver over **MSBuild** (the build engine) + **Roslyn** (the C# compiler). The
project is **SDK-style** — a tiny `.csproj` that opts into a whole framework:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">      <!-- the Web SDK: ASP.NET Core, Kestrel, etc. -->
  <TargetFramework>net8.0</TargetFramework>  <!-- runtime + BCL version -->
  <Nullable>enable</Nullable>                <!-- non-null reference types -->
  <ImplicitUsings>enable</ImplicitUsings>    <!-- auto-import common namespaces -->
```

([Billing.Api.csproj](../../src/Billing.Api/Billing.Api.csproj)). `Microsoft.NET.Sdk.Web` is why there
are **zero `<PackageReference>`s** — ASP.NET Core comes from the SDK's *framework reference*, not NuGet
packages. There's no `Directory.Build.props`, `Directory.Packages.props`, `global.json`, or
`nuget.config` — so any installed .NET 8+ SDK builds it, with no central package management.

### NuGet (by its absence)

Normally NuGet restores `<PackageReference>` packages into the build. Here the only "packages" are the
framework, so `dotnet restore` has almost nothing to do. **When you add EF Core (S-1)**, that's when
NuGet enters: `dotnet add package Microsoft.EntityFrameworkCore.Sqlite` writes a `<PackageReference>`
and pins a version — and you'd likely add `Directory.Packages.props` for central version management.

### Build & run

| Command | Does |
| --- | --- |
| `dotnet build` | MSBuild → Roslyn → IL assembly in `bin/` |
| `dotnet run --project src/Billing.Api` | build + launch Kestrel on `:5088` ([launchSettings L8](../../src/Billing.Api/Properties/launchSettings.json#L8)) |
| `dotnet test` | build + run the (currently nonexistent) test project |

All **inferred** — `dotnet` is not installed on the authoring host (see [verification-log](../08-reference/verification-log.md)).

### Configuration layering

`appsettings.json` → `appsettings.{Environment}.json` → environment variables, last wins. The
environment is `Development` ([launchSettings L11](../../src/Billing.Api/Properties/launchSettings.json#L11)),
so [appsettings.Development.json](../../src/Billing.Api/appsettings.Development.json) overlays the
[base](../../src/Billing.Api/appsettings.json). The `Billing` section binds to `BillingOptions`.

## Frontend side

### npm & the lockfile

[package.json](../../src/Billing.Web/package.json) declares deps (`react`, `react-dom`) and devDeps
(`@vitejs/plugin-react`, `typescript`, `vite`). `npm install` resolves them into `node_modules` and
writes a `package-lock.json` (**not committed** here — a real project would commit it for reproducible
installs).

### Vite (dev server + bundler)

[vite.config.ts](../../src/Billing.Web/vite.config.ts) wires the React plugin and pins the dev port to
5173 with `strictPort: false` ([L8](../../src/Billing.Web/vite.config.ts#L8)) — meaning **if 5173 is
taken, Vite silently picks another port**, which breaks the hardcoded CORS allowlist (the
[CORS debugging scenario](../05-quality-engineering/03-systematic-debugging.md)). Scripts
([package.json L6-L10](../../src/Billing.Web/package.json#L6-L10)):

| Script | Does |
| --- | --- |
| `npm run dev` | Vite dev server with HMR |
| `npm run build` | **`tsc -b` (type-check) then `vite build`** (bundle) |
| `npm run preview` | serve the built bundle |

Note `build` runs the TypeScript compiler *first* — types are checked at build time even though Vite
itself strips types without checking. So a type error fails the build but **not** `npm run dev`.

### TypeScript config

[tsconfig.json](../../src/Billing.Web/tsconfig.json): `strict: true` ([L10](../../src/Billing.Web/tsconfig.json#L10)),
`noEmit: true` ([L16](../../src/Billing.Web/tsconfig.json#L16)) (Vite emits, not `tsc`),
`jsx: "react-jsx"` (the modern transform — no `import React` needed), `moduleResolution: "Node"`,
ES2022 target. `import.meta.env.VITE_API_BASE_URL` ([api.ts L1](../../src/Billing.Web/src/api.ts#L1)) is
Vite's typed env mechanism — only `VITE_`-prefixed vars are exposed to the client.

### Env & secrets

[.env.example](../../src/Billing.Web/.env.example) documents `VITE_API_BASE_URL`; real `.env*` files are
gitignored ([.gitignore L8-L10](../../.gitignore#L8-L10)). The frontend secret-handling pattern is
correct; the *backend* signing secret is the gap (R-09).

## The two builds, side by side

```
C#:   .cs  ──Roslyn──►  IL  ──CLR JIT──►  machine code      (one self-contained process, Kestrel)
TS:   .tsx ──tsc(check)─► (types ok)  ──Vite/esbuild──► JS bundle ──► browser
```

## Drill

What's the minimum to get *both* running from a clean clone? List the commands in order and mark each
verified/inferred. **Self-grade — Basic:** `dotnet run` + `npm install && npm run dev`. **Solid:** you
note prerequisites (.NET 8, Node 20) and the ports (5088/5173) and the CORS coupling. **Strong:** you
flag that `npm run dev` skips type-checking (only `npm run build` runs `tsc`), so a type error can hide
in dev — and that all `dotnet` commands here are inferred (no SDK on the authoring host).

## Verification Notes

Project/config contents read-verified on 2026-06-09. `node` (v22.16.0) and `npm` (10.9.2) presence were
**verified**; `dotnet` is absent; a frontend `npm install` was attempted and failed on disk space — so
all build commands are **inferred**. Full detail in the [verification-log](../08-reference/verification-log.md).
