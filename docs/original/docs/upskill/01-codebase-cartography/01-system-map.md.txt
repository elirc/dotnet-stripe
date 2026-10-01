# System Map

## Project Graph

| Area | Ownership | Evidence | Public/private |
| --- | --- | --- | --- |
| Solution | One API project | [SubscriptionBillingDashboard.sln](../../../SubscriptionBillingDashboard.sln#L1-L19) references `src\Billing.Api\Billing.Api.csproj`. | Developer-facing. |
| API runtime | ASP.NET Core minimal API | Service registration and pipeline in [Program.cs](../../../src/Billing.Api/Program.cs#L7-L37). | Public HTTP under `/api` from [Program.cs](../../../src/Billing.Api/Program.cs#L37-L250). |
| Domain | Billing records/enums/money | [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123), [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L3-L45), [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57). | Internal model exposed by some API responses. |
| Contracts | Request/response DTOs | Requests in [Requests.cs](../../../src/Billing.Api/Contracts/Requests.cs#L5-L38), responses in [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L5-L71). | Public JSON contract. |
| Persistence | In-memory dictionaries/queues | Dictionaries and queues in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17). | Private boundary; API consumes methods. |
| Workers | Payment and webhooks | Registered in [Program.cs](../../../src/Billing.Api/Program.cs#L19-L20), implemented in [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L36) and [WebhookDeliveryWorker.cs](../../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6-L56). | Private side-effect runners. |
| React client | Single-page dashboard | Entry in [main.tsx](../../../src/Billing.Web/src/main.tsx#L1-L10), main UI in [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L395), API client in [api.ts](../../../src/Billing.Web/src/api.ts#L145-L184). | Browser-facing. |

## Runtime Boundaries

Browser state is local React state in [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L47). Browser writes go through `postJson`, which injects idempotency keys in [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158). Server writes enter minimal API endpoints in [Program.cs](../../../src/Billing.Api/Program.cs#L67-L250). Background workers run in the same API process because they are hosted services in [Program.cs](../../../src/Billing.Api/Program.cs#L19-L20).

## Missing Boundaries To Notice

No separate data project exists; persistence is process-local in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17). No test project exists in the file inventory; test strategy must start from current seams such as [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L7-L204) and endpoint mapping in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L250). No auth services or middleware are registered near [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35); label authorization assumptions as risks.

