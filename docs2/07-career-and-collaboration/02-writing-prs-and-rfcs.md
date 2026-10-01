> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/07-career-and-collaboration/02-writing-prs-and-rfcs.md.txt).

# 07.02 — Writing PRs & RFCs

A PR is a persuasion document: you're asking a busy maintainer to take on the risk of your change. An
RFC is the same for a *design*. Both win on clarity and honesty about trade-offs.

## PR template (use for every ticket in module 06)

```markdown
## What
One sentence: the behavior change.

## Why
The problem / ticket. Link it.

## How
The approach in 2–4 bullets. Name the pattern you followed (e.g. "mirrors the GET /api/plans shape").

## How tested
Commands run + result. If you couldn't run something, say so explicitly.
e.g. `dotnet test --filter FullyQualifiedName~InvoiceTotals` → 3 passed.

## Risks / blast radius
What could this break? Which files/flows does it touch? Is it reversible?

## Follow-ups
Anything deliberately left out, with a ticket reference.
```

### A worked example (Good-First Ticket: add `GET /api/coupons/{id}`)

```markdown
## What
Add a read-only `GET /api/coupons/{id:guid}` lookup endpoint.

## Why
The API can list coupons but not fetch one by id; the UI and tests need single-coupon lookup. (Ticket GF-3.)

## How
- Mirrors the existing `GET /api/coupons` shape (Program.cs L122) and reuses
  `BillingStore.TryGetCoupon` (BillingStore.cs L142) — no new store method.
- Returns the same `NotFound { error }` envelope used by the subscriptions endpoint (Program.cs L159).

## How tested
- `dotnet run` then `curl /api/coupons` to get a seeded id, `curl /api/coupons/<id>` → 200 with coupon;
  `curl /api/coupons/<random-guid>` → 404. (Commands inferred — run after installing .NET 8.)

## Risks / blast radius
- Additive, read-only, one file (Program.cs). Reversible by deleting the route. No state touched.

## Follow-ups
- None. (A `GET /api/coupons/{id}` could later gain tenant scoping when auth lands — R-02.)
```

Notice: it names the pattern it copied, anchors it, states the blast radius as *small and reversible*,
and is honest that the test commands are inferred.

## Commit messages

- **Subject:** imperative, ≤ ~60 chars: `Add GET /api/coupons/{id} lookup`.
- **Body:** *why*, not *what the diff already shows*. Wrap ~72 chars.
- **One logical change per commit.** Don't mix a rename with a behavior change — it makes review and
  revert hard.

```
Add GET /api/coupons/{id} lookup

The API could list coupons but not fetch one by id. Reuses the existing
TryGetCoupon accessor and the standard NotFound envelope so the contract
matches the rest of /api. Read-only and additive.
```

## RFC template (repo-tailored — use for module 06 senior projects)

```markdown
# RFC: <title>   (e.g. "Persist billing state in EF Core")

## Summary
2–3 sentences: what changes and why now.

## Motivation
The problem in this repo, anchored. (e.g. "BillingStore is in-memory; restart loses all invoices —
R-08, BillingStore.cs L8-L17.")

## Goals / Non-goals
Goals: durability, transactions. Non-goals: auth (separate RFC), multi-currency (R-01, deferred).

## Design
The approach. For this repo, always address:
- The store seam (IBillingStore) and the ~20 call sites it touches.
- The Money mapping (two columns vs owned type).
- DI lifetime: the captive-dependency trap (singleton engine + scoped DbContext).
- Transaction boundaries for multi-write ops (GenerateInvoice's 4 writes).
- Indexes the in-memory version never needed.

## Alternatives considered
The "do nothing / keep in-memory" option and why it loses now (link the switch signal from
Comparison 1).

## Migration & rollback
Characterization tests first; ship behind a flag; flip per environment; revert path.

## Testing strategy
What proves it didn't change behavior (the $1,111.20 invoice stays $1,111.20).

## Risks & open questions
The honest list.
```

## What makes maintainers say yes

1. **Small blast radius, clearly stated.** "One file, additive, reversible" lowers their risk.
2. **It follows an existing pattern** — you point at the pattern.
3. **It's tested** (or you're honest that it isn't and why).
4. **The trade-offs are *yours*, surfaced** — don't make the reviewer discover the downside.

## Drill

Write the PR description for one good-first ticket you've done, using the template. Then delete every
sentence that just restates the diff. **Self-grade — Basic:** all sections filled. **Solid:** the
"Risks" section is specific and the test commands are real or honestly marked inferred. **Strong:**
the "Why" would convince a maintainer who's never seen the ticket, and the "Follow-ups" shows you know
what you deliberately didn't do.

## Verification Notes

Templates are illustrative; the embedded code references (Program.cs L122, TryGetCoupon L142, etc.)
are read-verified on 2026-06-09 against the actual files.
