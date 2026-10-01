# Trace Tables

## Trace: UI To API Dashboard

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [App.tsx](../../../src/Billing.Web/src/App.tsx#L53-L71) | no args | React | effect calls `refresh` | no cancellation |
| 2 | [api.ts](../../../src/Billing.Web/src/api.ts#L145-L168) | `DashboardResponse` | API client | fetch JSON | runtime trust |
| 3 | [Program.cs](../../../src/Billing.Api/Program.cs#L41-L63) | store slices | API | aggregate response | unbounded read |
| 4 | [Responses.cs](../../../src/Billing.Api/Contracts/Responses.cs#L38-L71) | invoice DTOs | Contracts | maps money/lines | raw domain still exposed elsewhere |

## Trace: Usage To Invoice

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [App.tsx](../../../src/Billing.Web/src/App.tsx#L133-L142) | subscriptionId/quantity | React | records usage | string-to-number |
| 2 | [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202) | `CreateUsageEventRequest` | API | validates and creates event | no event dedupe |
| 3 | [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L122-L129) | `UsageEvent` | Store | dictionary insert | in-memory only |
| 4 | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L44-L60) | unbilled usage | Engine | billable quantity | included-unit edge cases |

## Trace: Payment

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [App.tsx](../../../src/Billing.Web/src/App.tsx#L152-L154) | invoice ID | React | pay POST | repeated clicks |
| 2 | [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221) | route GUID | API | queues payment | auth missing |
| 3 | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L154) | pending payment/job | Engine | save and enqueue | queue loss |
| 4 | [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L15-L35) | job | Worker | apply success | always succeeds |

## Trace: Auth/Permission Gap

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [Program.cs](../../../src/Billing.Api/Program.cs#L9-L20) | services | API | registers no auth service | unauthenticated writes |
| 2 | [Program.cs](../../../src/Billing.Api/Program.cs#L32-L35) | middleware | API | no auth middleware | no principal |
| 3 | [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221) | invoice ID | API | queues payment | IDOR if deployed |

## Trace: Error Path

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [Program.cs](../../../src/Billing.Api/Program.cs#L180-L183) | invalid quantity | API | `BadRequest` | error shape ad hoc |
| 2 | [api.ts](../../../src/Billing.Web/src/api.ts#L160-L177) | non-2xx response | Client | throws `Error` | assumes JSON error |
| 3 | [App.tsx](../../../src/Billing.Web/src/App.tsx#L73-L84) | exception | React | sets banner | no field-level errors |

## Trace: Webhook

| Step | File | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [Program.cs](../../../src/Billing.Api/Program.cs#L223-L250) | raw body | API | verifies and deserializes | no event-id persistence |
| 2 | [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L38) | timestamp/signature/body | Security | HMAC compare | direct system time |
| 3 | [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L199) | payment result | Engine | updates invoice/payment | late event ordering |

