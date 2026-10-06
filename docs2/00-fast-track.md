# 00 — Fast Track: your first weekend

Goal: in a few hours, go from "never opened this repo" to "I ran it, I traced one feature, I made
one safe change, and I can explain what I skipped." Everything here is a real command or a real
anchor. Commands are tagged **[inferred]** when they were *not* run on the authoring host (no .NET
SDK, no working `npm install` there — see [verification-log](08-reference/verification-log.md)). Run
them yourself; confirming them is the point.

## 0. What you're about to run

Two processes: a **.NET 8 API** ([src/Billing.Api](../src/Billing.Api)) on `http://localhost:5088`
and a **React/Vite SPA** ([src/Billing.Web](../src/Billing.Web)) on `http://localhost:5173`. The API
holds all state **in memory** — there is no database. Restarting the API resets everything to the
seed data in [BillingStore.Seed](../src/Billing.Api/Infrastructure/BillingStore.cs#L267-L321).

## 1. Install & run (do this first)

> **Status (2026-10-06): this step fails on `main` as committed.** `dotnet run` stops at two compile errors (CS0246 in
> `Infrastructure/IdempotencyMiddleware.cs`, CS0173 at `Program.cs:143`). On a scratch branch, add
> `using Billing.Api.Domain;` as the first line of `IdempotencyMiddleware.cs` and change `? null :` to
> `? (Money?)null :` on `Program.cs:143`. Then continue. Explaining *why* each fix works is `learn/00-onramp.md`
> exercise 1; don't skip it.

Prerequisites (per [README.md](../README.md#L14-L19)): **.NET SDK 8+** and **Node 20+**.

**Backend** — `[inferred]` (correct per [Billing.Api.csproj](../src/Billing.Api/Billing.Api.csproj#L3) and [launchSettings.json](../src/Billing.Api/Properties/launchSettings.json#L8)):

```powershell
dotnet run --project src/Billing.Api
# serves http://localhost:5088 ; ASPNETCORE_ENVIRONMENT=Development
```

**Frontend** — `[inferred]` (correct per [package.json](../src/Billing.Web/package.json#L6-L10)):

```powershell
cd src/Billing.Web
npm install
npm run dev      # vite on http://localhost:5173 (vite.config.ts pins port 5173)
```

The SPA targets the API via `VITE_API_BASE_URL`, defaulting to `http://localhost:5088`
([api.ts](../src/Billing.Web/src/api.ts#L1)). CORS only allows the two `localhost:5173` origins
([Program.cs](../src/Billing.Api/Program.cs#L21-L30)) — if you change the Vite port, the browser
calls will fail CORS.

## 2. One check to confirm it's alive

`[inferred]` — the health endpoint is anonymous and has no dependencies
([Program.cs](../src/Billing.Api/Program.cs#L39)):

```powershell
curl http://localhost:5088/api/health
# -> {"status":"ok","timestamp":"..."}
```

Then hit the endpoint the whole UI is built on:

```powershell
curl http://localhost:5088/api/dashboard
# one fat JSON: customers, plans, coupons, subscriptions, invoices, payments,
# usageEvents, webhookEvents, metrics  (DashboardResponse)
```

If `/api/dashboard` returns seed customers "Acme Analytics" and "Northstar Labs", you're wired up.

## 3. Two flows to trace (pick one this weekend)

Open these in [01/05-key-flows.md](01-codebase-cartography/05-key-flows.md) and follow the trace tables:

- **Flow 2 — Metered billing cycle** *(recommended; it has a fully hand-worked total you can verify)*:
  clicking **"Run cycle"** in the UI posts to `/api/simulator/run-cycle`, which calls
  [BillingEngine.RunBillingCycle](../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127) →
  [GenerateInvoice](../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L113). For the seeded
  "Scale Usage" subscription this produces an invoice of **USD 1111.20** (base $249 + 114,000
  billable units × $0.01, minus a 20% coupon). The flows doc shows the arithmetic.
- **Flow 4 — Inbound signed webhook**: `POST /api/webhooks/payment-provider` is HMAC-verified
  ([WebhookSignatureVerifier](../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L40))
  then applied ([ApplyPaymentResult](../src/Billing.Api/Infrastructure/BillingEngine.cs#L157-L200)).

## 4. The first 10 files, in order

Read these top-to-bottom before touching anything. (Full annotated list in
[01/02-file-reading-order.md](01-codebase-cartography/02-file-reading-order.md).)

| # | File | Why / what to notice |
| --- | --- | --- |
| 1 | [README.md](../README.md) | Claims and intent; note line 57 — the store is the production boundary. |
| 2 | [Program.cs](../src/Billing.Api/Program.cs) | The whole API surface in one file: DI ([L9-L30](../src/Billing.Api/Program.cs#L9-L30)) + every endpoint. |
| 3 | [BillingEnums.cs](../src/Billing.Api/Domain/BillingEnums.cs) | The state vocabulary — invoice/subscription/payment statuses. |
| 4 | [BillingModels.cs](../src/Billing.Api/Domain/BillingModels.cs) | The domain nouns as immutable records. |
| 5 | [Money.cs](../src/Billing.Api/Domain/Money.cs) | The single most important value type; integer minor units. |
| 6 | [BillingStore.cs](../src/Billing.Api/Infrastructure/BillingStore.cs) | The "database": dictionaries behind one lock; the seed data. |
| 7 | [BillingEngine.cs](../src/Billing.Api/Infrastructure/BillingEngine.cs) | All the business rules: invoicing, payment, period advance. |
| 8 | [IdempotencyMiddleware.cs](../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs) | The one cross-cutting concern; replay vs 409. |
| 9 | [api.ts](../src/Billing.Web/src/api.ts) | The client contract — types mirror the C# DTOs. |
| 10 | [App.tsx](../src/Billing.Web/src/App.tsx) | The entire UI: state, effects, forms, tables. |

## 5. One small, safe change (junior-grade)

**Add a `GET /api/coupons/{id}` lookup endpoint.** It's safe because it's read-only, additive, and
follows an existing pattern (`GET /api/plans` style), with a tiny blast radius.

1. Read the existing `GET /api/coupons` at [Program.cs](../src/Billing.Api/Program.cs#L122) and the
   store accessor [TryGetCoupon](../src/Billing.Api/Infrastructure/BillingStore.cs#L142-L145).
2. Add, right after the coupons GET:

```csharp
// Illustrative: not from this repo — your change to add
api.MapGet("/coupons/{id:guid}", (Guid id, BillingStore store) =>
    store.TryGetCoupon(id, out var coupon)
        ? Results.Ok(coupon)
        : Results.NotFound(new { error = $"Unknown coupon: {id}" }));
```

3. Confirm: `curl http://localhost:5088/api/coupons` to grab a seeded coupon id, then
   `curl http://localhost:5088/api/coupons/<that-id>`.

**Why this shape is right:** it reuses `TryGet*` instead of re-implementing a lookup, returns the
same `NotFound` envelope the rest of the API uses (e.g.
[subscriptions](../src/Billing.Api/Program.cs#L157-L160)), and adds no state. **What would get it
rejected:** returning `Results.Ok(null)` on a miss (leaks a 200 for a 404), or bypassing the store's
lock by reading a dictionary directly.

## 6. One teach-back (prove you understood)

In 5 sentences, answer: *"Where does an invoice's money come from, and why can't it be a `double`?"*
A solid answer names [GenerateInvoice](../src/Billing.Api/Infrastructure/BillingEngine.cs#L62-L64)
(subtotal → discount → total), the `Money` integer-minor-unit representation
([Money.cs](../src/Billing.Api/Domain/Money.cs#L7-L16)), and the reason: binary floating point can't
represent values like `$0.10` exactly, so summing many line items drifts — integers don't.

## 7. What this fast track deliberately skips

- **The whole auth story** — because there isn't one. Every endpoint is anonymous; there is no
  tenant scoping. That's the biggest lesson in the repo, but it's a *senior* lesson — see
  [03/03-validation-auth-and-permissions.md](03-architecture-and-patterns/03-validation-auth-and-permissions.md).
- **Concurrency and idempotency edge cases** — the happy path replays cleanly; the race does not.
  See [03/04](03-architecture-and-patterns/04-side-effects-async-and-reliability.md).
- **The latent currency bug** — the dashboard math assumes USD; a non-USD plan can throw. See
  [risk-register R-01](08-reference/risk-register.md).
- **Tests** — there are none (no test project, no CI workflow; re-checked 2026-10-06). You'll write the first ones in
  [05/02-writing-tests-here.md](05-quality-engineering/02-writing-tests-here.md).

## Verification Notes

- File list, line anchors, and the seed-data invoice arithmetic are **read-verified / hand-traced**
  from source on 2026-06-09.
- All `dotnet`/`npm`/`curl` commands are **inferred** — not executed on the authoring host
  (`dotnet` absent; `npm install` failed with ENOSPC). See
  [verification-log](08-reference/verification-log.md).
