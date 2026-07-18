# Command Cheatsheet

| Command | Purpose | Status | Evidence |
| --- | --- | --- | --- |
| `rg --files` | Inventory repository files. | Verified | Used for this suite; file inventory includes API/client/docs. |
| `git -C C:\Users\Owner status --short -- Desktop/.netstripe` | Scope git status to this repo folder. | Verified | Repo is under a larger worktree; this avoids profile traversal. |
| `dotnet run --project src/Billing.Api` | Run API. | Inferred, not verified | README command [README.md](../../../README.md#L22-L27), launch URL [launchSettings.json](../../../src/Billing.Api/Properties/launchSettings.json#L1-L13). |
| `dotnet build SubscriptionBillingDashboard.sln` | Build API. | Inferred, not verified | Solution references API [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19). |
| `dotnet test` | Run future tests. | Not available yet | No test project in [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19). |
| `cd src/Billing.Web && npm install` | Restore frontend packages. | Not run by request | Dependencies in [package.json](../../../src/Billing.Web/package.json#L11-L19). |
| `cd src/Billing.Web && npm run dev` | Run Vite dev server. | Inferred, not verified | Script in [package.json](../../../src/Billing.Web/package.json#L6-L8). |
| `cd src/Billing.Web && npm run build` | Type-check and bundle. | Inferred, not verified | Script in [package.json](../../../src/Billing.Web/package.json#L8-L9). |
| `Invoke-RestMethod http://localhost:5088/api/health` | Probe API health. | Inferred, not verified | Endpoint in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L39). |

No Docker, migration, lint, format, or codegen commands were found in project metadata. The frontend package has only `dev`, `build`, and `preview` scripts in [package.json](../../../src/Billing.Web/package.json#L6-L10).

