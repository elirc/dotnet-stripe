# 02.02 — Framework Mental Models

Two frameworks, two mental models: the ASP.NET Core request pipeline (+ DI) and the React render model.

## ASP.NET Core: the middleware pipeline

A request flows through an ordered chain of middleware; each can short-circuit or call the next. This
app's chain ([Program.cs L32-L37](../../src/Billing.Api/Program.cs#L32-L37)):

```
request → UseCors("dashboard")  →  UseMiddleware<IdempotencyMiddleware>  →  routing → endpoint lambda
```

**Order is behavior.** CORS runs first (so cross-origin preflight is handled before anything). The
idempotency middleware runs *before* the endpoint so it can **replay a cached response without ever
invoking the handler** ([IdempotencyMiddleware L35-L49](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L35-L49)).
A middleware is just a class with `InvokeAsync(HttpContext, …)` that calls `next` — exactly the shape of
[`IdempotencyMiddleware.InvokeAsync`](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9).
**Missing from the chain:** `UseExceptionHandler` and any auth middleware — which is why R-01 is a raw
500 and why everything is anonymous.

## Dependency injection & lifetimes

Services are registered in the container ([Program.cs L9-L20](../../src/Billing.Api/Program.cs#L9-L20))
and *injected* where needed (constructor params, or endpoint-lambda params via
[parameter injection, e.g. `BillingStore store`](../../src/Billing.Api/Program.cs#L65)).

Three lifetimes:

| Lifetime | Created | Used here |
| --- | --- | --- |
| **Singleton** | once for the app | **everything** — store, engine, clock, idempotency store, verifier ([L14-L18](../../src/Billing.Api/Program.cs#L14-L18)) |
| **Scoped** | once per request | none today (would be the `DbContext` after S-1) |
| **Transient** | every resolve | none |

Everything is singleton **on purpose**: the store *is* the shared state, so there must be exactly one.

### The captive dependency bug (why this matters later)

If you inject a **shorter-lived** service into a **longer-lived** one, the longer-lived one "captures"
the first instance forever. Today it can't happen (everything's singleton). But the moment S-1 adds a
**scoped** `DbContext` and someone injects it into the **singleton** `BillingEngine`, that one context
is captured across all requests → corrupted change-tracking. The fix: make the engine scoped, or have
the workers open a scope per tick. This is the #1 DI interview question and a real trap for *this*
migration ([Comparison 1](../03-architecture-and-patterns/07-comparative-implementations.md), step 5).

## Model binding & validation

ASP.NET binds the JSON body to the handler's parameter type. `POST /api/customers` binds to
[`CreateCustomerRequest`](../../src/Billing.Api/Contracts/Requests.cs#L5) automatically
([Program.cs L67](../../src/Billing.Api/Program.cs#L67)). Notes:

- Enums bind from **strings** because of [`JsonStringEnumConverter`](../../src/Billing.Api/Program.cs#L10-L13)
  — so `"Monthly"` binds to `BillingCycle.Monthly` ([CreatePlanRequest L7-L15](../../src/Billing.Api/Contracts/Requests.cs#L7-L15)).
- Validation here is **manual** (in-handler `if` checks), not attribute/`DataAnnotations`-based. A bind
  *failure* (non-JSON, wrong type) produces a framework 400 with a *different* envelope than the app's
  `{error}` shape — a small inconsistency worth knowing.

## Options pattern

`builder.Services.Configure<BillingOptions>(...)` ([L9](../../src/Billing.Api/Program.cs#L9)) binds the
`Billing` config section to a typed class; consumers inject `IOptions<BillingOptions>`
([verifier L7](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L7)). `IOptions<T>` is a
singleton snapshot — config changes need `IOptionsMonitor<T>` to hot-reload. (Pattern card in
[03/05](../03-architecture-and-patterns/05-pattern-catalog.md).)

## React: render vs commit

React **renders** (calls your component function to compute the next UI) then **commits** (applies the
diff to the DOM). Your component must be a pure function of props + state. [`App`](../../src/Billing.Web/src/App.tsx#L27)
renders from `dashboard` state; when state changes (`setDashboard`), React re-renders.

## Hooks, effects & cleanup

- **`useState`** holds local state — one big `dashboard` object plus form state
  ([L28-L47](../../src/Billing.Web/src/App.tsx#L28-L47)).
- **`useEffect`** runs *after* commit, for side effects. The first effect loads data once
  (`[]` deps, [L53-L55](../../src/Billing.Web/src/App.tsx#L53-L55)); the second sets default form
  selections when `dashboard` changes ([L57-L62](../../src/Billing.Web/src/App.tsx#L57-L62)).
- **Cleanup:** neither effect returns a cleanup function — fine here (no subscriptions/timers to tear
  down), but it's the slot where you'd cancel a fetch or clear a timer.

## Stale closures, keys, memoization

- **Stale closure (avoided):** the default-selection effect uses the **updater form**
  `setSubscriptionCustomerId(current => current || …)` ([L59](../../src/Billing.Web/src/App.tsx#L59)) so it
  reads the *latest* value, not a captured one. Reading the state variable directly inside the effect
  would capture a stale value — a classic React bug this code sidesteps.
- **Keys:** every list render uses a stable `key={item.id}` ([e.g. invoices L275](../../src/Billing.Web/src/App.tsx#L275))
  so React can diff efficiently. Using an array index would break on reorder.
- **Memoization:** `useMemo` builds lookup maps once per dashboard change
  ([`customersById` etc. L49-L51](../../src/Billing.Web/src/App.tsx#L49-L51)) instead of rebuilding on
  every render — the right use of `useMemo` (a derived computation keyed on its input).

## Data fetching

Hand-rolled: a `fetch` wrapper ([api.ts request L160-L169](../../src/Billing.Web/src/api.ts#L160-L169))
plus a "refetch the whole dashboard after every write" rule ([runAction L73-L84](../../src/Billing.Web/src/App.tsx#L73-L84)).
Simple and always-consistent; no caching/dedup (the React Query trade-off in
[Comparison 3](../03-architecture-and-patterns/07-comparative-implementations.md)).

## Drill

Trace one state update: clicking "Run cycle". Which function fires, what `setState` runs, why does the
table update? **Self-grade — Basic:** `runBillingCycle` → POST → `refresh()` → `setDashboard`.
**Solid:** you note the re-render flows from the new `dashboard` object, and the `useMemo` maps rebuild
because their dep changed. **Strong:** you explain why there's no live update — the UI only reflects the
server after the explicit refetch, and what React Query would change.

## Verification Notes

Pipeline order, DI registrations, hook usages, and the stale-closure-avoidance are read-verified on
2026-06-09. React render/commit semantics are standard framework knowledge applied to this code; the app
was not run.
