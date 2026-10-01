# Tooling And Build System

## .NET Build

The solution file points to `src\Billing.Api\Billing.Api.csproj` in [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19). The project uses `Microsoft.NET.Sdk.Web` and targets .NET 8 in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L1-L7).

**Useful inferred commands:**

| Command | Purpose | Evidence |
| --- | --- | --- |
| `dotnet restore SubscriptionBillingDashboard.sln` | Restore API packages. | Solution project path in [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19). |
| `dotnet build SubscriptionBillingDashboard.sln` | Compile API. | SDK project in [Billing.Api.csproj](../../../src/Billing.Api/Billing.Api.csproj#L1-L7). |
| `dotnet run --project src/Billing.Api` | Run API. | README command in [README.md](../../../README.md#L22-L27). |

## Frontend Build

Vite scripts are in [package.json](../../../src/Billing.Web/package.json#L6-L10), React dependencies in [package.json](../../../src/Billing.Web/package.json#L11-L19), and Vite config in [vite.config.ts](../../../src/Billing.Web/vite.config.ts#L1-L10).

**Useful inferred commands:**

| Command | Purpose | Evidence |
| --- | --- | --- |
| `npm install` | Restore frontend packages. | Dependencies in [package.json](../../../src/Billing.Web/package.json#L11-L19). |
| `npm run dev` | Run Vite dev server. | Script in [package.json](../../../src/Billing.Web/package.json#L6-L8). |
| `npm run build` | Type-check and bundle. | Script in [package.json](../../../src/Billing.Web/package.json#L8-L9). |

## Tooling Gaps

No `global.json` exists to pin SDK selection; verify local SDK before build. No lockfile was present, so frontend dependency resolution can drift from ranges in [package.json](../../../src/Billing.Web/package.json#L11-L19). No formatter/linter scripts are present in [package.json](../../../src/Billing.Web/package.json#L6-L10). No CI workflow files appeared in `rg --files`; see [../08-reference/verification-log.md](../08-reference/verification-log.md).

