> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../original/docs/upskill/00-fast-track.md.txt).

# Fast Track: Weekend Win

## Commands

| Goal | Command | Status | Evidence |
| --- | --- | --- | --- |
| Run API | `dotnet run --project src/Billing.Api` | Inferred, not verified | README lists it in [README.md](../../README.md#L22-L27); launch profile sets `http://localhost:5088` in [launchSettings.json](../../src/Billing.Api/Properties/launchSettings.json#L1-L13). |
| Run web | `cd src/Billing.Web && npm install && npm run dev` | Inferred, not executed | Package scripts are in [package.json](../../src/Billing.Web/package.json#L6-L10). User requested no installs. |
| API health | `Invoke-RestMethod http://localhost:5088/api/health` | Inferred | Endpoint is mapped in [Program.cs](../../src/Billing.Api/Program.cs#L39-L39). |
| Build API | `dotnet build SubscriptionBillingDashboard.sln` | Inferred, not verified | Solution references the API project in [SubscriptionBillingDashboard.sln](../../SubscriptionBillingDashboard.sln#L1-L19). |

## First 10 Files

1. [README.md](../../README.md#L1-L54): product intent and commands.
2. [Program.cs](../../src/Billing.Api/Program.cs#L7-L37): app composition and pipeline.
3. [BillingModels.cs](../../src/Billing.Api/Domain/BillingModels.cs#L3-L123): domain nouns.
4. [Money.cs](../../src/Billing.Api/Domain/Money.cs#L5-L57): money invariant.
5. [BillingStore.cs](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L264): in-memory persistence boundary.
6. [BillingEngine.cs](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204): invoice/payment behavior.
7. [IdempotencyMiddleware.cs](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L7-L88): request replay boundary.
8. [PaymentWorker.cs](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L36): background payment simulation.
9. [api.ts](../../src/Billing.Web/src/api.ts#L145-L184): browser API client.
10. [App.tsx](../../src/Billing.Web/src/App.tsx#L27-L395): UI flows.

## Weekend Flow To Trace

Trace “Run cycle”: button click [App.tsx](../../src/Billing.Web/src/App.tsx#L145-L149), typed POST helper [api.ts](../../src/Billing.Web/src/api.ts#L149-L158), endpoint [Program.cs](../../src/Billing.Api/Program.cs#L204-L209), engine cycle [BillingEngine.cs](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124), invoice construction [BillingEngine.cs](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L81), store writes [BillingEngine.cs](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105).

## Small Safe Change

Add a validation message for empty usage metric. Read current request shape in [Requests.cs](../../src/Billing.Api/Contracts/Requests.cs#L25-L25), current endpoint defaulting in [Program.cs](../../src/Billing.Api/Program.cs#L185-L196), and UI sender in [App.tsx](../../src/Billing.Web/src/App.tsx#L133-L142). Keep blast radius to one endpoint and one UI form.

## Teach-Back

Explain why `Money` rejects blank currency in [Money.cs](../../src/Billing.Api/Domain/Money.cs#L18-L27), why API writes should have `Idempotency-Key` in [api.ts](../../src/Billing.Web/src/api.ts#L149-L158), and why replay checks hash method/path/body in [IdempotencyMiddleware.cs](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L45).

## What This Skips

It skips EF Core, auth, test harness, CI, Docker, and production deployment because this repo does not contain those surfaces. Treat them as design exercises anchored to the current in-memory store [BillingStore.cs](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17), app registration [Program.cs](../../src/Billing.Api/Program.cs#L9-L20), and package metadata [package.json](../../src/Billing.Web/package.json#L6-L19).

