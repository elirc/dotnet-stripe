# 04.02 — Trace Tables

Trace tables make you track *value shapes* as data crosses boundaries — where a string becomes a
number becomes a `Money`, who owns it, and what could go wrong at each hop. Four traces covering the
four boundary types: **UI→API, persistence, auth, and error propagation.**

Columns: **Step · Owner · File:line · Value shape · Transformation · Risk.**

---

## Trace 1 (UI → API): Create a plan, "99" → `Money(9900,"USD")`

| Step | Owner | File:line | Value shape | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | UI form | [App.tsx L32-L41](../../src/Billing.Web/src/App.tsx#L32-L41) | `{basePrice:"99", unitPrice:"0.01", ...}` (all **strings**) | user typing | numeric fields are strings until coerced |
| 2 | UI submit | [App.tsx L97-L102](../../src/Billing.Web/src/App.tsx#L97-L102) | `{basePrice: 99, unitPrice: 0.01}` (**numbers**) | `Number(planForm.basePrice)` | `Number("")` → `NaN`; `Number("1e3")` → `1000` |
| 3 | Wire | fetch body | JSON `{"basePrice":99,...}` | `JSON.stringify` | — |
| 4 | Model binding | [Program.cs L87](../../src/Billing.Api/Program.cs#L87) | `CreatePlanRequest { BasePrice = 99m }` (**decimal**) | JSON → record | a non-numeric → 400 bind error (different envelope than the app's `{error}`) |
| 5 | Validation | [L94-L97](../../src/Billing.Api/Program.cs#L94-L97) | same | reject if `< 0` | no upper bound (R-12) |
| 6 | Money build | [L108](../../src/Billing.Api/Program.cs#L108) | `Money(9900,"USD")` (**minor units**) | `FromMajor(99m,"USD")` → `round(99×100)` | `MidpointRounding.AwayFromZero` ([Money L14](../../src/Billing.Api/Domain/Money.cs#L14)) |
| 7 | Store insert | [AddPlan L82-L87](../../src/Billing.Api/Infrastructure/BillingStore.cs#L82-L87) | dict entry | uniqueness scan on `Code` | linear scan; race if this were a DB without a unique index |
| 8 | Response | [L114](../../src/Billing.Api/Program.cs#L114) | `201` + raw `Plan` | entity serialized | `basePrice` → `{minorUnits:9900,currency:"USD",majorUnits:99.00}` (raw entity, no DTO) |
| 9 | Client type | [api.ts MoneyValue L37-L41](../../src/Billing.Web/src/api.ts#L37-L41) | `MoneyValue` | parsed | TS shape must match the C# property names by hand |

**Lesson:** the value is a *string* in the browser, a *number* on the wire, a *decimal* at binding,
and an *integer* (`Money`) in the domain. Each hop is a place a bug can enter.

---

## Trace 2 (Persistence): Usage rollup inside invoice generation

Tracing how 8 seeded usage events become one `$1,140.00` line.

| Step | Owner | File:line | Value shape | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Engine | [BillingEngine L46](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L46) | call with `(subId, periodStart, periodEnd)` | request unbilled usage | period comes from the *subscription*, not `billingDate` |
| 2 | Store | [GetUnbilledUsage L182-L188](../../src/Billing.Api/Infrastructure/BillingStore.cs#L182-L188) | `IReadOnlyList<UsageEvent>` (8 rows) | `Where(InvoiceId==null && OccurredAt∈[start,end))` + `OrderBy` | **full scan**; usage outside the window silently excluded |
| 3 | Engine | [L49](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L49) | `int 214000` | `Sum(x => x.Quantity)` | `int` overflow if quantities were huge (R-12) |
| 4 | Engine | [L50](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L50) | `int 114000` | `max(0, 214000 − 100000)` | included-units not pro-rated |
| 5 | Engine | [L53-L58](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L53-L58) | `InvoiceLine` amount `Money(114000,"USD")` | `plan.UnitPrice.Multiply(114000)` | rounding away-from-zero per line |
| 6 | Engine | [L83](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L83) | 8 rows with `InvoiceId` set | `x with { InvoiceId = invoice.Id }` + save | **separate write** from the invoice save — not atomic |

**Lesson:** the "billed" flag (`InvoiceId`) is the thing preventing double-billing usage; it's set in
a *different* store call than the invoice save, so a crash between them double-bills or loses the line
(R-08).

---

## Trace 3 (Auth): An inbound webhook with a bad signature

| Step | Owner | File:line | Value shape | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Endpoint | [Program.cs L225-L228](../../src/Billing.Api/Program.cs#L225-L228) | raw body string + headers | buffer + read + rewind | body read twice (also by mw) |
| 2 | Verifier | [IsValid L13-L17](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L13-L17) | header values | require both headers | missing → `false` |
| 3 | Verifier | [L19-L28](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L19-L28) | `long` unix ts | parse + tolerance check | uses `DateTimeOffset.UtcNow` directly |
| 4 | Verifier | [L30-L34](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L30-L34) | `expected` hex digest | `HMAC(secret, "{ts}.{body}")` | secret defaults to `dev_secret_replace_me` (R-09) |
| 5 | Verifier | [L37-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37-L39) | `bool false` | `FixedTimeEquals(expected, actual)` | constant-time — correct |
| 6 | Endpoint | [L230-L233](../../src/Billing.Api/Program.cs#L230-L233) | `401 Unauthorized` | early return | **no logging** of the rejected attempt (observability gap) |

**Lesson:** the signature *is* the authentication for this endpoint. A wrong secret, a mangled body,
or a stale timestamp all collapse to one `false` → `401`. Note step 6 logs nothing — you'd be blind
to an attack.

---

## Trace 4 (Error propagation): The currency-mismatch 500 (R-01)

Tracing what happens when a EUR plan has an active subscription and someone loads the dashboard.

| Step | Owner | File:line | Value shape | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Endpoint | [Program.cs L41-L51](../../src/Billing.Api/Program.cs#L41-L51) | lists of entities | gather + `CalculateMetrics` | — |
| 2 | Metrics | [L262](../../src/Billing.Api/Program.cs#L262) | `sum = Money(0,"USD")` | seed accumulator in USD | **hardcoded currency** |
| 3 | Metrics | [L269-L273](../../src/Billing.Api/Program.cs#L269-L273) | `monthlyAmount = Money(x,"EUR")` | read `plan.BasePrice` (EUR) | mismatch incoming |
| 4 | Money | [Add → EnsureSameCurrency L65](../../src/Billing.Api/Domain/Money.cs#L65) | — | `throw InvalidOperationException("Currency mismatch: USD vs EUR")` | the throw |
| 5 | Pipeline | [Program.cs L32-L37](../../src/Billing.Api/Program.cs#L32-L37) | exception propagates | **no `UseExceptionHandler`** | becomes a raw `500` (hypothesis — not run) |
| 6 | UI | [App.tsx refresh L64-L70](../../src/Billing.Web/src/App.tsx#L64-L70) | `error` state set | `catch` → show "Load failed" | the *entire* dashboard is down, not one row |

**Lesson:** a single non-USD active subscription propagates a domain exception all the way to a 500
that blanks the whole UI. The blast radius of a "data variation" is "the app is down" — that's why
R-01 is ranked so high. (Step 5 is labeled a hypothesis: there's no global handler registered, but it
wasn't executed.)

## Drill

Pick Trace 1 and inject a fault: what does the system do if the user types `basePrice = "1e3"`? Walk
each row. **Self-grade — Basic:** `Number("1e3")` → `1000`. **Solid:** it sails through as a valid
$1,000 plan (no upper bound). **Strong:** you note the UI's `type="number"` input mostly prevents this,
so the *server* is the real validation boundary and currently under-validates (R-12).

## Verification Notes

All traces hand-traced from source on 2026-06-09. Trace 4 step 5 (raw 500) is a **hypothesis** — no
global exception handler is registered, but the code was not executed. Trace 1 step 8's wire shape
(`{minorUnits,currency,majorUnits}`) is inferred from the `Money` public properties + ASP.NET web JSON
defaults; the property set is read-verified, the serialized casing was not run.
