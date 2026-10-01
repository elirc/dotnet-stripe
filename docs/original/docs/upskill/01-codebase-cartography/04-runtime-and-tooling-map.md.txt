# Runtime And Tooling Map

## .NET

The API targets `net8.0` with nullable reference types and implicit usings enabled in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L1-L7). Runtime configuration binds the `Billing` section in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L9) to options defined in [BillingOptions.cs](../../../src/Billing.Api/Infrastructure/BillingOptions.cs#L3-L8) and configured in [appsettings.json](../../../src/Billing.Api/appsettings.json#L1-L11).

## ASP.NET Runtime

Middleware order is CORS then idempotency in [Program.cs](../../../src/Billing.Api/Program.cs#L32-L37). Endpoints are minimal API handlers under `/api` in [Program.cs](../../../src/Billing.Api/Program.cs#L37-L250). Hosted services run in-process with the web server because they are registered in [Program.cs](../../../src/Billing.Api/Program.cs#L19-L20).

## React/Vite

The client uses Vite with React plugin in [vite.config.ts](../../../src/Billing.Web/vite.config.ts#L1-L10), TypeScript strict mode in [tsconfig.json](../../../src/Billing.Web/tsconfig.json#L1-L20), and scripts in [package.json](../../../src/Billing.Web/package.json#L6-L10). The API base URL defaults in [api.ts](../../../src/Billing.Web/src/api.ts#L1-L1) and can be overridden with [`.env.example`](../../../src/Billing.Web/.env.example#L1-L1).

## Environment And Secrets

Webhook signing secret is currently a dev value in [appsettings.json](../../../src/Billing.Api/appsettings.json#L3-L9). The verifier consumes it through options in [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L7-L38). Production rule: never ship real secrets in repo files; use environment variables or a secret store.

## Verification Notes

No lockfile was present in the inventory; package resolution is not pinned beyond semver ranges in [package.json](../../../src/Billing.Web/package.json#L11-L19). No `global.json` was present, so SDK selection is machine-dependent despite the project targeting `net8.0` in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L3-L3).

