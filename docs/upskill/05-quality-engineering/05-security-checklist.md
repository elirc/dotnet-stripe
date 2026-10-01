> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../../astraupskill/README.md) for executable guidance. [Exact original text](../../original/docs/upskill/05-quality-engineering/05-security-checklist.md.txt).

# Security Checklist

| Check | Current evidence | Risk | Pre-merge question |
| --- | --- | --- | --- |
| Authentication | Not registered in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35) | Unauthenticated access | Who is the caller? |
| Authorization/IDOR | Endpoints accept customer/subscription/invoice IDs in [Program.cs](../../../src/Billing.Api/Program.cs#L155-L221) | Cross-tenant access | How is resource ownership checked? |
| Input validation | Manual checks in [Program.cs](../../../src/Billing.Api/Program.cs#L67-L202) | Missing field-specific validation | What invariant owns this? |
| CORS | Local origins only in [Program.cs](../../../src/Billing.Api/Program.cs#L21-L30) | Misunderstood as auth | Is API protected beyond browser CORS? |
| Webhook authenticity | HMAC in [WebhookSignatureVerifier.cs](../../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L38) | Replay/event dedupe gaps | Is provider event ID persisted? |
| Secrets | Dev secret in [appsettings.json](../../../src/Billing.Api/appsettings.json#L3-L9) | Secret leakage if real | Is this value environment-sourced? |
| XSS | React renders text values in tables [App.tsx](../../../src/Billing.Web/src/App.tsx#L262-L358) | React escapes by default | Are any future HTML injections needed? |
| CSRF | API uses JSON/fetch, no cookie auth shown in [api.ts](../../../src/Billing.Web/src/api.ts#L149-L158) | Future cookie auth needs CSRF | What auth mechanism is chosen? |
| SQL injection | No SQL/EF present; store is dictionaries in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L7-L17) | Future migration risk | Are queries parameterized? |
| Rate limiting | Not configured near [Program.cs](../../../src/Billing.Api/Program.cs#L21-L35) | Abuse of billing writes | What endpoints need limits? |
| Open redirects/SSRF | No redirect or external HTTP sender; webhook destination placeholder in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L100-L113) | Future sender SSRF | Who controls destination URL? |
| Dependency risk | Dependencies listed in [package.json](../../../src/Billing.Web/package.json#L11-L19) | No lockfile found | Are dependencies pinned and scanned? |

## Senior Drill

Threat-model `POST /api/webhooks/payment-provider` from [Program.cs](../../../src/Billing.Api/Program.cs#L223-L250). Include spoofing, replay, tampering, denial-of-service, and state-transition confusion.

