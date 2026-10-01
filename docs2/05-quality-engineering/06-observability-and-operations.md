> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/05-quality-engineering/06-observability-and-operations.md.txt).

# 05.06 — Observability & Operations

The operational question for every flow: **"How would I know this broke in production?"** Today, for
most flows, the honest answer is *"I wouldn't"* — so this page maps both what exists and the gaps.

## What exists

| Signal | Where | Covers |
| --- | --- | --- |
| `ILogger` | injected into both workers ([PaymentWorker L11](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L11), [WebhookDeliveryWorker L10](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L10)) | a dropped payment job ([L31](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L31)); a failed delivery ([L42](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L42)) |
| Log levels | [appsettings.Development.json](../../src/Billing.Api/appsettings.Development.json) | `Information` default, ASP.NET at `Warning` |
| Health check | [`GET /api/health`](../../src/Billing.Api/Program.cs#L39) | liveness only — returns ok + timestamp |
| Dashboard state | `WebhookEvent.Status/Attempt/LastError` surfaced in the UI ([App.tsx L335-L356](../../src/Billing.Web/src/App.tsx#L335-L356)) | manual eyeballing of delivery status |

## What's missing (the gaps)

- **No metrics.** No counters for invoices generated, payments succeeded/failed, webhooks delivered. You
  can't graph or alert.
- **No tracing.** No OpenTelemetry; you can't follow one request across the endpoint → engine → worker
  hops.
- **No structured error tracking.** No global exception handler ([Program.cs L32-L37](../../src/Billing.Api/Program.cs#L32-L37)),
  so an unhandled error (R-01) is a raw 500 with no captured context.
- **Health check is shallow.** `/api/health` doesn't check the store or worker liveness — it returns ok
  even if the workers have crashed.
- **The endpoints log nothing.** No request logging on the API surface; a rejected webhook signature
  ([401, L232](../../src/Billing.Api/Program.cs#L232)) leaves no trace — you'd be blind to an attack.
- **`LastError` is misleading.** It's set to a note on the *success* path
  ([worker L35](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L35)), so an operator
  reading it can't trust it (R-06).

## "How would I know?" per flow

| Flow | If it broke, today you'd find out by… | What you should add |
| --- | --- | --- |
| Invoice generation | a user noticing a wrong/missing invoice | a counter of invoices generated per cycle + the total amount; alert on zero during a scheduled run |
| Payment processing | a user noticing an unpaid invoice; the dropped-job warning log | success/failure counters; alert on a rising failure rate |
| Webhook delivery | eyeballing the dashboard's webhook table | a "rows stuck `Pending` > N minutes" alert (the real signal once delivery can fail) |
| Inbound webhook | nothing — 401s and 200s are silent | log every inbound attempt (accepted/rejected) with the event id |
| Dashboard 500 (R-01) | users reporting a blank dashboard | a global exception handler that logs the exception + returns a clean problem-detail |

## A minimal observability starter (when you build it)

1. **A global exception handler** (`app.UseExceptionHandler`) that logs and returns RFC-7807
   problem-details — this alone would turn R-01 from a silent 500 into a captured, alertable event.
2. **Counters** (via `System.Diagnostics.Metrics` / OpenTelemetry): `invoices_generated`,
   `payments_succeeded`, `payments_failed`, `webhooks_delivered`, `webhooks_failed`.
3. **A deeper health check** that verifies the store responds and the workers are alive.
4. **Request logging** on the `/api` group, including the webhook accept/reject decision.

These map directly to the failure modes the [risk register](../08-reference/risk-register.md) already
identified — observability is how you'd *catch* R-01, R-05, R-06, R-07 in production instead of from
an angry customer.

## Drill

Pick the payment flow. Write the three signals (a metric, a log line, an alert) you'd add so that a
spike in failed payments pages you within minutes. **Self-grade — Basic:** you add a failure log.
**Solid:** a `payments_failed` counter + a threshold alert. **Strong:** you also add the *context*
(invoice id, provider, failure reason) so the page is actionable, and you note that today the worker
*always succeeds*, so the failure path is currently untestable until a real provider is wired in.

## Verification Notes

Existing signals (the two logger call sites, the health endpoint, log config, the UI webhook table) are
read-verified on 2026-06-09. The "missing" list was verified by the absence of any
metrics/tracing/exception-handler registration in [Program.cs](../../src/Billing.Api/Program.cs#L7-L37).
Nothing was executed.
