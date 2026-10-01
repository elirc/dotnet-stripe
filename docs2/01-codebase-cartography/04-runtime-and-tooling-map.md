> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/01-codebase-cartography/04-runtime-and-tooling-map.md.txt).

# 01.04 — Runtime & Tooling Map

What actually runs, what builds it, and where the boundaries between processes are.

## The two runtimes

| | Backend | Frontend |
| --- | --- | --- |
| Runtime | .NET 8 (CLR) | Browser JS engine (V8 etc.) via the dev server; Node for tooling |
| Project type | `Microsoft.NET.Sdk.Web` ([csproj L1](../../src/Billing.Api/Billing.Api.csproj#L1)) | Vite 6 + React 19 ([package.json](../../src/Billing.Web/package.json)) |
| Target | `net8.0` ([csproj L3](../../src/Billing.Api/Billing.Api.csproj#L3)) | `ES2022`, DOM libs ([tsconfig L3-L5](../../src/Billing.Web/tsconfig.json#L3-L5)) |
| Entry | [Program.cs](../../src/Billing.Api/Program.cs) (top-level statements) | [main.tsx](../../src/Billing.Web/src/main.tsx) → `createRoot(...).render(<App/>)` |
| Port | `http://localhost:5088` ([launchSettings L8](../../src/Billing.Api/Properties/launchSettings.json#L8)) | `http://localhost:5173` ([vite.config L7](../../src/Billing.Web/vite.config.ts#L7)) |
| State | In-memory, lost on restart | React component state, lost on refresh |

## Build & run toolchain

| Tool | Role | Where configured |
| --- | --- | --- |
| **.NET SDK 8** | `dotnet build` / `run`; drives MSBuild + Roslyn (C# compiler) | [Billing.Api.csproj](../../src/Billing.Api/Billing.Api.csproj) |
| **MSBuild** | Resolves the SDK-style project; `ImplicitUsings` and `Nullable` are on ([csproj L4-L5](../../src/Billing.Api/Billing.Api.csproj#L4-L5)) | csproj `<PropertyGroup>` |
| **NuGet** | Package restore. **No explicit `PackageReference`** — everything comes from the `Web` SDK's framework reference. There is no `Directory.Packages.props`/`nuget.config`. | (implicit) |
| **Node 20+ / npm** | Frontend dependency install + script runner | [package.json scripts L6-L10](../../src/Billing.Web/package.json#L6-L10) |
| **Vite 6** | Dev server (HMR) + production bundler | [vite.config.ts](../../src/Billing.Web/vite.config.ts) |
| **TypeScript 5.8** | Type-checks (`tsc -b`) before bundling; `strict: true` | [tsconfig.json](../../src/Billing.Web/tsconfig.json) |

There is **no** `global.json` (so any installed .NET 8+ SDK is used), no lockfile committed for the
frontend, no CI workflow, no Dockerfile, no Makefile. (Verified by file listing.)

## Process boundaries (where bugs hide)

```
┌─ Browser process ───────────┐        ┌─ .NET process (Kestrel) ─────────────────┐
│ React render + state        │  HTTP  │  CORS → Idempotency → endpoint            │
│ api.ts fetch wrapper        │ ─────► │  ↳ BillingEngine ↔ BillingStore (memory)  │
│ Intl money formatting       │ ◄───── │                                          │
└─────────────────────────────┘  JSON  │  ┌─ Worker threads (same process) ─────┐ │
                                        │  │ PaymentWorker (PeriodicTimer 4s)    │ │
   Provider (external) ── HTTP ───────► │  │ WebhookDeliveryWorker (8s)          │ │
   POST /api/webhooks/payment-provider  │  └─────────────────────────────────────┘ │
                                        └──────────────────────────────────────────┘
```

Three boundaries to keep straight:

1. **Browser ↔ API (network + serialization).** JSON casing flips: C# `PascalCase` records →
   camelCase on the wire (configured by ASP.NET's web defaults; the TS types in
   [api.ts](../../src/Billing.Web/src/api.ts#L16-L143) are camelCase). Enums are serialized as
   *strings* because of [`JsonStringEnumConverter`](../../src/Billing.Api/Program.cs#L10-L13) — that's
   why `InvoiceStatus` compares as `"Open"` in [App.tsx L281](../../src/Billing.Web/src/App.tsx#L281).
2. **Endpoint ↔ background worker (in-process, async).** The HTTP handler enqueues
   ([QueuePayment](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L155)) and returns
   `202 Accepted`; the worker finishes the job later. The client only learns the result on the next
   dashboard refresh.
3. **API ↔ external provider (HMAC-signed HTTP).** Outbound webhooks are *simulated* (no real
   network); inbound webhooks are real HTTP gated by signature.

## Configuration & secrets (high level)

| Source | Holds | Anchor |
| --- | --- | --- |
| `appsettings.json` | The `Billing` section: signing secret, worker intervals, idempotency TTL | [appsettings.json L3-L8](../../src/Billing.Api/appsettings.json#L3-L8) |
| `appsettings.Development.json` | Log levels only | [file](../../src/Billing.Api/appsettings.Development.json) |
| `BillingOptions` | Strongly-typed binding of that section, with defaults | [BillingOptions](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L3-L9) |
| `.env` (frontend) | `VITE_API_BASE_URL` | [.env.example](../../src/Billing.Web/.env.example) (`.env` gitignored) |

**Secret hygiene note (teaching, not a runtime claim):** the webhook signing secret defaults to
`dev_secret_replace_me` and is committed in `appsettings.json` ([L4](../../src/Billing.Api/appsettings.json#L4)).
For a real deployment this moves to a secret store / environment variable and is never committed.
`.gitignore` already excludes `.env*` ([gitignore L8-L10](../../.gitignore#L8-L10)),
so the frontend pattern is right; the backend secret is the gap.

## "How would I observe this running?"

- **Backend logs:** `ILogger` is injected into both workers
  ([PaymentWorker L11](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L11),
  [WebhookDeliveryWorker L10](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L10)). Log
  level is `Information` by default. There are **no health checks beyond `/api/health`**, no metrics,
  no tracing. See [05/06-observability-and-operations.md](../05-quality-engineering/06-observability-and-operations.md).
- **Frontend:** the only signal is the dashboard re-render after `runAction`
  ([App.tsx L73-L84](../../src/Billing.Web/src/App.tsx#L73-L84)) and the network panel.

## Verification Notes

Ports, target frameworks, config keys, and tooling versions are read-verified from the config files
on 2026-06-09. The JSON-casing/enum-string behavior is inferred from the ASP.NET web defaults plus
the `JsonStringEnumConverter` registration — *the registration is read-verified; the runtime casing
was not executed here.* `dotnet`/`vite` were not runnable on the authoring host.
