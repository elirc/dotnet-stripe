> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/01-codebase-cartography/02-file-reading-order.md.txt).

# 01.02 — File Reading Order

Read in this order. Each row says *what to look for*, not just *what it is* — the point is to read
with a question in mind. The repo is small (≈25 source files), so this covers essentially all of it.

## Tier 1 — Orientation (everyone, in this order)

| # | File | Read for |
| --- | --- | --- |
| 1 | [README.md](../../README.md) | The claims. Note L57: the in-memory store is *the* production boundary. |
| 2 | [docs/architecture.md](../../docs/architecture.md) | The author's own list of what a production migration needs — a free risk checklist. |
| 3 | [BillingEnums.cs](../../src/Billing.Api/Domain/BillingEnums.cs) | The state vocabulary. Notice `SubscriptionStatus.PastDue` ([L19](../../src/Billing.Api/Domain/BillingEnums.cs#L19)) — does anything ever set it? |
| 4 | [BillingModels.cs](../../src/Billing.Api/Domain/BillingModels.cs) | The nouns as immutable `record`s. Notice `Invoice` carries its own `Lines`, `Subtotal`, `Discount`, `Total` ([L78-L92](../../src/Billing.Api/Domain/BillingModels.cs#L78-L92)). |
| 5 | [Money.cs](../../src/Billing.Api/Domain/Money.cs) | The most important type. `readonly record struct`, integer minor units, currency guarded in the ctor ([L18-L27](../../src/Billing.Api/Domain/Money.cs#L18-L27)). |
| 6 | [Program.cs](../../src/Billing.Api/Program.cs) | The whole API in one screen. Read DI ([L9-L30](../../src/Billing.Api/Program.cs#L9-L30)) then skim every `Map*`. This is your table of contents. |

## Tier 2 — Junior depth (make small changes confidently)

| # | File | Read for |
| --- | --- | --- |
| 7 | [Contracts/Requests.cs](../../src/Billing.Api/Contracts/Requests.cs) | The exact POST bodies. These records *are* the model binding target. |
| 8 | [Contracts/Responses.cs](../../src/Billing.Api/Contracts/Responses.cs) | The DTO/entity split — `InvoiceResponse.From` ([L53-L73](../../src/Billing.Api/Contracts/Responses.cs#L53-L73)) maps domain → wire. Why does a DTO exist? |
| 9 | [BillingStore.cs](../../src/Billing.Api/Infrastructure/BillingStore.cs) | Dictionaries behind one `lock` ([L7](../../src/Billing.Api/Infrastructure/BillingStore.cs#L7)). The uniqueness checks ([L68](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68), [L82](../../src/Billing.Api/Infrastructure/BillingStore.cs#L82)) are linear scans — fine for a dict, a table-scan in a DB. Read `Seed` ([L267-L321](../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L321)) so the dashboard numbers make sense. |
| 10 | [api.ts](../../src/Billing.Web/src/api.ts) | The TS types mirror the C# DTOs by hand. `postJson` auto-adds an `Idempotency-Key` ([L154](../../src/Billing.Web/src/api.ts#L154)). |
| 11 | [App.tsx](../../src/Billing.Web/src/App.tsx) | The entire UI. Find the two `useEffect`s ([L53-L62](../../src/Billing.Web/src/App.tsx#L53-L62)) and the `runAction` refresh-after-write pattern ([L73-L84](../../src/Billing.Web/src/App.tsx#L73-L84)). |

## Tier 3 — Mid-level depth (cross-layer changes, tests, debugging)

| # | File | Read for |
| --- | --- | --- |
| 12 | [BillingEngine.cs](../../src/Billing.Api/Infrastructure/BillingEngine.cs) | The business core. `GenerateInvoice` ([L9-L113](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L113)) is the densest method in the repo — line build, usage rollup, discount, period advance, webhook enqueue, all in one. |
| 13 | [BillingModels.cs `Coupon.CalculateDiscount`](../../src/Billing.Api/Domain/BillingModels.cs#L31-L49) | A domain method with a subtle cap. Try to restate the cap logic ([L46-L48](../../src/Billing.Api/Domain/BillingModels.cs#L46-L48)) in one sentence. |
| 14 | [IdempotencyMiddleware.cs](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs) | Replay vs 409 vs pass-through. Note: no key → no protection ([L17-L21](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L17-L21)); only 2xx is cached ([L63-L67](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L63-L67)). |
| 15 | [PaymentWorker.cs](../../src/Billing.Api/Infrastructure/PaymentWorker.cs) | A `BackgroundService` with a `PeriodicTimer` ([L16](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L16)) that always "succeeds" payment ([L40-L44](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L40-L44)). |
| 16 | [WebhookDeliveryWorker.cs](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs) | An "outbox" worker. Look hard at the `try` body ([L28-L39](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L28-L39)) — can it actually throw? If not, the retry path is dead. |
| 17 | [WebhookSignatureVerifier.cs](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs) | HMAC-SHA256 over `{timestamp}.{body}`, 5-min tolerance, constant-time compare ([L37-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37-L39)). Solid crypto; note no nonce/event-id dedup. |
| 18 | [BillingOptions.cs](../../src/Billing.Api/Infrastructure/BillingOptions.cs) + [appsettings.json](../../src/Billing.Api/appsettings.json) | The 4 knobs. Default signing secret is `dev_secret_replace_me` ([L5](../../src/Billing.Api/Infrastructure/BillingOptions.cs#L5)). |

## Tier 4 — Senior depth (critique, risk, design)

| # | File | Read for |
| --- | --- | --- |
| 19 | [Program.cs `CalculateMetrics`](../../src/Billing.Api/Program.cs#L254-L290) | MRR/outstanding aggregated in a hardcoded `"USD"` ([L262](../../src/Billing.Api/Program.cs#L262), [L276](../../src/Billing.Api/Program.cs#L276)). What happens with a EUR plan? (R-01.) |
| 20 | [BillingEngine.GenerateInvoice L34, L62](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L34-L64) | Invoice currency = `customer.Currency`, but line amounts come from `plan.BasePrice` (plan's currency). Same-currency assumption again. |
| 21 | [BillingEngine.RunBillingCycle](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127) | One invoice per run even if multiple periods elapsed — no catch-up. Proration? None. |
| 22 | [BillingEngine.ApplyPaymentResult](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200) | Failure sets invoice → `Open`, never `PastDue` ([L167](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L167)); creates a *new* `Payment` each call → not idempotent for retried webhooks. |
| 23 | [BillingStore.Seed](../../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L321) | Fixed GUIDs and a precomputed scenario. Useful for tests, but the seed *is* the only "fixture" the repo has. |
| 24 | [vite.config.ts](../../src/Billing.Web/vite.config.ts) + [tsconfig.json](../../src/Billing.Web/tsconfig.json) | `strict: true`, `strictPort: false`. The port can drift → CORS breakage. |

## How to use this list

- **Junior:** read 1–11, do one good-first ticket.
- **Mid:** add 12–18, write a test (module 05), run a debugging scenario.
- **Senior:** add 19–24, then go straight to the [critique](../03-architecture-and-patterns/06-architecture-critique.md)
  and [risk register](../08-reference/risk-register.md) and decide what you'd fix first.

## Verification Notes

Every file and line range here was opened and read on 2026-06-09. "Can it throw?" / "what happens
with EUR?" prompts are read-verified observations turned into questions; the answers live in modules
03 and 08.
