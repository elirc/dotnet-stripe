> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/03-architecture-and-patterns/07-comparative-implementations.md.txt).

# 03.07 — Comparative Implementations

The senior skill isn't knowing the "right" pattern — it's choosing between *viable* ones and knowing
what each choice commits the team to, who pays for it, and what signal means it's time to switch.

Three decision points this repo actually embodies one side of. For each: the repo's real (anchored)
choice vs a mainstream alternative, a side-by-side on concrete dimensions, a verdict, a "switch
signal", and a drill. Card 1 includes a full **migration drill** with rollback.

Where the alternative doesn't exist in this repo, it's a clearly-labeled `// Illustrative` sketch —
never presented as real.

---

## Comparison 1: In-memory `BillingStore` vs EF Core `DbContext`

**The decision this repo made:** persistence is a hand-rolled in-memory store — dictionaries +
queues behind one lock — with no database. —
[`BillingStore`](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17)

**The alternative:** an EF Core `DbContext` over a relational database, with entities mapped to tables
and queries expressed as LINQ-to-SQL.

```csharp
// Illustrative: not from this repo
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    // GetUnbilledUsage becomes:
    //   db.UsageEvents.Where(u => u.SubscriptionId == id && u.InvoiceId == null
    //                          && u.OccurredAt >= start && u.OccurredAt < end)
    //     .OrderBy(u => u.OccurredAt)  ← needs an index on (SubscriptionId, InvoiceId, OccurredAt)
}
```

**Side-by-side:**

| Dimension | This repo (in-memory store) | Alternative (EF Core) |
| --- | --- | --- |
| Complexity / ceremony | Low: 2 files, no migrations, no connection strings | High: provider, migrations, context lifetime, mapping config |
| Testability | Trivial — construct the store, it self-seeds ([L19-L22](../../src/Billing.Api/Infrastructure/BillingStore.cs#L19-L22)) | Needs a test DB or the in-memory/SQLite provider; change tracking surprises |
| Performance | Fast reads, but every list is an O(n) `.ToArray()` copy under a global lock; uniqueness checks are linear scans ([L68](../../src/Billing.Api/Infrastructure/BillingStore.cs#L68)) | Indexed lookups; but N+1 and lazy-loading traps; query translation overhead |
| Coupling / blast radius | The whole app depends on `BillingStore`'s concrete API; swapping it touches the engine + every endpoint | Depends on `DbContext` + LINQ; swapping providers is mostly config |
| Failure modes unique to it | **No durability** (restart = data loss); no real transactions across `Save*` calls; global-lock contention | Connection exhaustion; captive `DbContext` if injected into a singleton; migration drift; deadlocks |
| Migration cost (in THIS repo) | — | Medium: add EF, map 11 record types, write the initial migration, replace ~20 store call sites, manage context lifetime |

**What each choice commits you to / who pays:** the in-memory store buys *speed of learning* and pays
with *everything a real billing system needs* (durability, audit, concurrency, reporting at scale).
The team that ships this to production pays in lost invoices on the first restart. EF commits you to
schema migrations forever (a tax on every model change) and to understanding change-tracking and
context lifetime — the price of durability and queryability.

**Verdict for this repo:** **right call.** The README states the store is deliberately in-memory to
stay dependency-free for learning ([README L57](../../README.md#L57)). For its stated purpose, a DB
would be pure friction. The architecture even isolates the boundary so the swap is *possible*.

**Switch signal:** you'd revisit the moment any of these is true — data must survive a restart; more
than one process/replica serves traffic; you need ad-hoc reporting (the global-lock `.ToArray()`
dashboard won't scale); or you need real multi-write transactions (invoice + usage + period advance
must be all-or-nothing).

### Migration drill (write this plan on paper or a scratch branch — do NOT change product code)

Move this repo from `BillingStore` to EF Core + SQLite. Produce a step-by-step plan including tests
and rollback.

A strong plan covers, in order:

1. **Pin behavior first.** Write characterization tests against the *current* store/engine (e.g. the
   Flow 2 invoice = `$1,111.20`) so you can prove the migration didn't change results. *(There are no
   tests today — this step is non-negotiable.)*
2. **Introduce a seam.** Extract an `IBillingStore` interface from the current concrete class so the
   engine depends on the interface, not the implementation. Blast radius: the engine ctor + DI line
   ([Program.cs L15](../../src/Billing.Api/Program.cs#L15)).
3. **Map the model.** Decide how `Money` (a value object) maps — owned type or two columns
   (`MinorUnits`,`Currency`). Decide `Invoice.Lines` — owned collection or a child table.
4. **Add indexes the in-memory version never needed:** `(SubscriptionId, InvoiceId, OccurredAt)` for
   `GetUnbilledUsage`, unique indexes on `Customer.Email` and `Plan.Code` (today enforced by linear
   scan).
5. **Lifetime change.** `DbContext` is *scoped*, but the engine and workers are *singletons*
   ([Program.cs L14-L20](../../src/Billing.Api/Program.cs#L14-L20)). Injecting a scoped context into a
   singleton is the **captive dependency** bug — you must either make the engine scoped (and resolve
   it per request) or have the workers create a scope per tick via `IServiceScopeFactory`.
6. **Transactions.** Wrap the 3 writes in `GenerateInvoice` in one `SaveChanges`/transaction so they're
   atomic — something the in-memory version silently lacked.
7. **Rollback strategy.** Keep `IBillingStore` with *both* implementations behind a feature flag/config
   switch; ship EF dark; flip per-environment; if invoices diverge, flip back instantly. Because step
   1 pinned behavior, "diverge" is detectable.

**Self-grade — Basic:** you list "add EF, make a context, swap the store." **Solid:** you include the
seam, the indexes, and characterization tests. **Strong:** you call out the captive-dependency
lifetime trap *and* the now-needed transaction boundary *and* a flag-based rollback — i.e. you
identified the two things that bite *this* codebase specifically.

---

## Comparison 2: Minimal APIs vs MVC Controllers

**The decision this repo made:** every endpoint is a lambda registered on a route group in one file.
— [`Program.cs` MapGroup + Map* L37-L250](../../src/Billing.Api/Program.cs#L37-L250)

**The alternative:** controller classes with attribute routing and action methods.

```csharp
// Illustrative: not from this repo
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(BillingStore store, IBillingClock clock) : ControllerBase
{
    [HttpPost]
    public IActionResult Create(CreateCustomerRequest request) { /* same logic */ }
}
```

**Side-by-side:**

| Dimension | This repo (minimal APIs) | Alternative (controllers) |
| --- | --- | --- |
| Complexity / ceremony | Very low: handler is a lambda; deps are parameters | More files, base class, attributes; familiar to MVC teams |
| Testability | Handlers are hard to unit-test in isolation (they're inline lambdas); you test via `WebApplicationFactory` | Action methods are plain methods — directly unit-testable |
| Performance | Slightly leaner pipeline | Negligible difference in practice |
| Coupling / blast radius | All 12 endpoints in one 297-line file — every change touches `Program.cs` | Endpoints split by resource; smaller per-file blast radius |
| Failure modes unique to it | One giant file becomes a merge-conflict magnet; cross-cutting concerns (validation, auth) get copy-pasted per lambda | Convention sprawl; "fat controller" anti-pattern; filters add indirection |
| Migration cost (in THIS repo) | — | Low-medium: mechanical move of 12 handlers into ~6 controllers; DI is already constructor-friendly |

**What each choice commits you to / who pays:** minimal APIs commit you to *terseness* — great while
the surface is small, painful when validation/auth/filters need to apply uniformly and you're
hand-rolling them in every lambda (you can already see the repeated `catch (Exception ex) when (...)`
blocks, e.g. [customers](../../src/Billing.Api/Program.cs#L79-L82) and
[plans](../../src/Billing.Api/Program.cs#L116-L119)). Controllers commit you to MVC's conventions and
more files, paid back when you want per-resource organization, action filters, and easy unit tests.

**Verdict for this repo:** **defensible, trending toward "watch it".** At 12 endpoints minimal APIs
are clearly fine. The repeated exception-mapping and validation boilerplate is the early smell that
you're outgrowing inline lambdas.

**Switch signal:** you'd revisit when the same validation/auth/error-mapping appears in 3+ handlers
(extract a filter or move to controllers), when `Program.cs` crosses a few hundred lines of routing,
or when you need per-endpoint unit tests without spinning up the host.

**Drill:** extract the duplicated `catch (InvalidOperationException or ArgumentException) → BadRequest`
into one reusable piece (an endpoint filter or a small helper). Count how many handlers it
de-duplicates. **Self-grade — Basic:** you move it to a helper method. **Solid:** an
`IEndpointFilter`. **Strong:** you argue whether this nudges you toward controllers or whether a
filter keeps minimal APIs viable — with a line count to back it.

---

## Comparison 3: Hand-rolled fetch state vs React Query

**The decision this repo made:** the SPA manages server data by hand — one `useState` for the whole
dashboard, a manual `refresh()`, and a "refetch everything after any write" rule. —
[`App.tsx` state + refresh L28-L84](../../src/Billing.Web/src/App.tsx#L28-L84),
[`api.ts` fetch wrapper L160-L169](../../src/Billing.Web/src/api.ts#L160-L169)

**The alternative:** a server-state library (TanStack/React Query) that owns caching, loading/error
state, and invalidation.

```tsx
// Illustrative: not from this repo
const { data, isLoading, error } = useQuery({ queryKey: ["dashboard"], queryFn: getDashboard });
const createCustomer = useMutation({
  mutationFn: (c) => postJson("/api/customers", c),
  onSuccess: () => queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
});
```

**Side-by-side:**

| Dimension | This repo (hand-rolled) | Alternative (React Query) |
| --- | --- | --- |
| Complexity / ceremony | Low: no dependency; `useState` + `useEffect` + `refresh()` | A library + provider; more concepts (query keys, cache, staleness) |
| Testability | Component + fetch wrapper; easy to mock `fetch` | Query hooks need a `QueryClientProvider` in tests |
| Performance | Refetches the *entire* `/api/dashboard` after every action ([runAction L77-L78](../../src/Billing.Web/src/App.tsx#L77-L78)) — simple but wasteful | Granular cache + background refetch; dedupes in-flight requests |
| Coupling / blast radius | All server state lives in one `dashboard` object; every consumer reads through it | Cache is keyed/global; invalidation is explicit |
| Failure modes unique to it | No request dedup (double-click can fire two writes); no caching; `busy` flag is the only concurrency guard ([L73-L83](../../src/Billing.Web/src/App.tsx#L73-L83)); stale data between refreshes | Cache invalidation bugs; over-fetching if keys are wrong; library version churn |
| Migration cost (in THIS repo) | — | Low: one screen, one query, ~6 mutations; mechanical |

**What each choice commits you to / who pays:** hand-rolling commits you to re-implementing
loading/error/refetch logic per screen and to "refetch the world" performance — fine for a
single-screen demo, costly as screens multiply (every developer re-derives the pattern, slightly
differently). React Query commits you to a dependency and its mental model, paid back the moment you
have multiple data sources, want optimistic updates, or care about over-fetching.

**Verdict for this repo:** **right call for now.** It's a single dashboard screen; a server-state
library would be more concept than benefit. The "refetch everything" rule is honest and bug-resistant.

**Switch signal:** you'd revisit when there's more than one screen sharing data, when the full-dashboard
refetch becomes visibly slow, or when you need optimistic UI / request dedup (e.g. to stop a
double-click creating two customers, which today only the server's email-uniqueness check prevents).

**Drill:** the `busy` flag disables buttons during a write ([L196](../../src/Billing.Web/src/App.tsx#L196))
but `refresh()` itself isn't guarded against overlap. Construct a sequence where two refreshes race
and the UI shows stale data. Then state how React Query's request dedup would prevent it.
**Self-grade — Basic:** you describe the race. **Solid:** you note the last-write-wins on `setDashboard`.
**Strong:** you weigh whether a tiny in-component guard fixes it more cheaply than adopting a library —
and pick one with a reason.

---

## Using these in interviews

Three questions in [07/04-interview-prep-from-this-repo.md](../07-career-and-collaboration/04-interview-prep-from-this-repo.md)
are pulled straight from these cards ("you chose an in-memory store over a database here — defend it";
"when would minimal APIs stop being the right call?"; "why no React Query?"). Design-trade-off
questions dominate mid/senior interviews — rehearse the verdict *and* the switch signal, not just the
pros and cons.

## Verification Notes

Every "this repo's choice" anchor was read on 2026-06-09. The alternative sketches are illustrative
and labeled; none claim to exist in the repo. The captive-dependency point in Card 1 is a read-verified
consequence of the current singleton registrations ([Program.cs L14-L20](../../src/Billing.Api/Program.cs#L14-L20))
combined with EF's scoped `DbContext` default — it is *the* trap this specific migration would hit.
