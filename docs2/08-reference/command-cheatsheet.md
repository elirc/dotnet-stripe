> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/08-reference/command-cheatsheet.md.txt).

# Command Cheatsheet

Every command you need, tagged **✅ verified** (run on the authoring host, output in the
[verification-log](verification-log.md)) or **⚠️ inferred** (correct per the config files, but **not**
executed here — `dotnet` is absent and `npm install` hit a disk limit). Run the inferred ones yourself;
confirming them is the learner's first "verify, don't trust" exercise.

## Environment checks

| Command | Tag | Notes |
| --- | --- | --- |
| `git rev-parse HEAD` | ✅ | Fails — **repo has no commits** (log #1). |
| `git log --oneline` | ✅ | "no commits yet" (log #2). |
| `dotnet --version` | ✅ | `command not found` — **no .NET SDK on this host** (log #4). |
| `node --version` | ✅ | `v22.16.0` (log #5). |
| `npm --version` | ✅ | `10.9.2` (log #6). |

## Backend (.NET) — all ⚠️ inferred (no SDK here)

| Command | Does |
| --- | --- |
| `dotnet build` | Compile the API (`net8.0`, [csproj L3](../../src/Billing.Api/Billing.Api.csproj#L3)). |
| `dotnet run --project src/Billing.Api` | Run on `http://localhost:5088` ([launchSettings L8](../../src/Billing.Api/Properties/launchSettings.json#L8)). |
| `dotnet test` | Run tests — **none exist yet** (R-03); see [05/02](../05-quality-engineering/02-writing-tests-here.md) to add them. |
| `dotnet new xunit -o tests/Billing.Api.Tests` | Scaffold the first test project. |
| `dotnet test --filter "FullyQualifiedName~GenerateInvoice"` | Run only the invoice-total test. |

## Frontend (Node/Vite)

| Command | Tag | Does |
| --- | --- | --- |
| `npm install` (in `src/Billing.Web`) | ⚠️ | Restore deps. **Attempted here; failed ENOSPC** (log #9) — retry with free disk. |
| `npm run dev` | ⚠️ | Vite dev server on `:5173` ([package.json L7](../../src/Billing.Web/package.json#L7)). |
| `npm run build` | ⚠️ | `tsc -b && vite build` — type-check **then** bundle ([L8](../../src/Billing.Web/package.json#L8)). |
| `npm run preview` | ⚠️ | Serve the built bundle. |

## Manual API smoke tests — ⚠️ inferred

```powershell
# health (anonymous, no deps) — Program.cs L39
curl http://localhost:5088/api/health

# the payload the whole UI is built on
curl http://localhost:5088/api/dashboard

# create a customer (idempotent write) — Program.cs L67
curl -X POST http://localhost:5088/api/customers `
  -H "Content-Type: application/json" -H "Idempotency-Key: demo-key-123" `
  -d '{"name":"Demo Co","email":"demo@example.com","currency":"USD"}'

# run the billing cycle (generates invoices) — Program.cs L204
curl -X POST http://localhost:5088/api/simulator/run-cycle `
  -H "Content-Type: application/json" -H "Idempotency-Key: cycle-1" -d '{}'
```

### Signed inbound webhook (PowerShell)

The product doc has a working signer — reuse it: [docs/webhooks.md L30-L44](../../docs/webhooks.md#L30-L44).
It computes `HMAC-SHA256("{timestamp}.{body}", secret)` and posts with `X-Webhook-Timestamp` /
`X-Webhook-Signature`. The default secret is `dev_secret_replace_me` ([appsettings L4](../../src/Billing.Api/appsettings.json#L4); R-09).

## Repo exploration — ✅ verified

| Command | Result |
| --- | --- |
| `find src -name '*.cs' \| xargs wc -l` | `1483 total` (log #7) |
| `find src -name '*.ts' -o -name '*.tsx' \| xargs wc -l` | `604 total` (log #8) |

## Ports & URLs (quick reference)

| What | Where | Source |
| --- | --- | --- |
| API | `http://localhost:5088` | [launchSettings L8](../../src/Billing.Api/Properties/launchSettings.json#L8) |
| SPA | `http://localhost:5173` | [vite.config L7](../../src/Billing.Web/vite.config.ts#L7) |
| API base (client) | `VITE_API_BASE_URL` (default `:5088`) | [api.ts L1](../../src/Billing.Web/src/api.ts#L1) |
| CORS allowed origins | `:5173` only | [Program.cs L26](../../src/Billing.Api/Program.cs#L26) |

> **Gotcha:** Vite's `strictPort: false` ([vite.config L8](../../src/Billing.Web/vite.config.ts#L8)) means
> the SPA may land on a different port if 5173 is busy — which breaks CORS. If the dashboard won't load,
> check the actual Vite port first (see [05/03](../05-quality-engineering/03-systematic-debugging.md)).

## Verification Notes

Verified rows have real output in the [verification-log](verification-log.md) (numbered cross-refs).
Inferred rows are correct per the anchored config but were not executed (no .NET SDK; failed
`npm install`).
