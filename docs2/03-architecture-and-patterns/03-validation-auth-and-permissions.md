> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/03-architecture-and-patterns/03-validation-auth-and-permissions.md.txt).

# 03.03 — Validation, Auth & Permissions

This is the most important page in module 03, because the headline is an *absence*: **this system has
input validation but no authentication, no authorization, and no tenant isolation.** Understanding
exactly what's missing — and what it would take to add — is the senior lesson the repo is built to teach.

## Validation: what exists

Validation is hand-written at each endpoint, before constructing the domain object.

| Endpoint | Checks | Anchor |
| --- | --- | --- |
| Create customer | name & email non-blank | [L69-L72](../../src/Billing.Api/Program.cs#L69-L72) |
| Create plan | code & name non-blank; prices ≥ 0 | [L89-L97](../../src/Billing.Api/Program.cs#L89-L97) |
| Create coupon | code & name non-blank; at least one of percent/amount; ranges | [L126-L139](../../src/Billing.Api/Program.cs#L126-L139) |
| Create subscription | plan must exist | [L157-L160](../../src/Billing.Api/Program.cs#L157-L160) |
| Record usage | quantity > 0 | [L180-L183](../../src/Billing.Api/Program.cs#L180-L183) |

A second validation tier lives in the **domain/store**: `Money`'s constructor rejects blank currency
([Money L20-L23](../../src/Billing.Api/Domain/Money.cs#L20-L23)); the store enforces uniqueness and
referential existence ([`AddSubscription` EnsureExists L110-L115](../../src/Billing.Api/Infrastructure/BillingStore.cs#L110-L115)).
This two-tier split (cheap presence checks at the edge, invariants in the domain) is a good shape.

### Validation gaps (R-12)

- **Email format isn't validated** — only presence ([L69](../../src/Billing.Api/Program.cs#L69)). `"x"` passes.
- **Currency isn't validated** — `NormalizeCurrency` only uppercases ([L292-L293](../../src/Billing.Api/Program.cs#L292-L293)); `"ZZZ"` is accepted.
- **Usage quantity has no upper bound** ([L180](../../src/Billing.Api/Program.cs#L180)) — a 2-billion-unit event yields an absurd invoice.
- **Subscription create doesn't verify the customer exists at the endpoint** — it's caught later by the store's `EnsureExists` and surfaced as a 400, which is acceptable but inconsistent with the explicit plan check above it.

## Authentication: none

There is no authentication middleware, no `AddAuthentication`, no `[Authorize]`, no JWT/cookie
handling anywhere. The only thing resembling a credential is the **webhook HMAC signature**
([WebhookSignatureVerifier](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L40)),
which authenticates *the payment provider* on one endpoint — not users.

So every endpoint under [`/api`](../../src/Billing.Api/Program.cs#L37-L250) is **anonymous**.

## Authorization & tenant isolation: none (R-02)

There is no concept of an owner or tenant. Entities have no `TenantId`/`OwnerId`. Therefore:

- `GET /api/dashboard` returns **every** customer, subscription, and invoice in the system
  ([L41-L63](../../src/Billing.Api/Program.cs#L41-L63)) — there is no "my data".
- `POST /api/invoices/{id}/pay` lets any caller pay **any** invoice by id
  ([L211-L221](../../src/Billing.Api/Program.cs#L211-L221)).
- `POST /api/usage-events` records usage against **any** subscription id
  ([L178-L202](../../src/Billing.Api/Program.cs#L178-L202)).

Every id-bearing endpoint is an **IDOR** (Insecure Direct Object Reference): the only thing you need
to act on someone else's resource is its `Guid`.

### Junior misses vs senior checks

| A junior sees… | A senior also asks… |
| --- | --- |
| "The pay endpoint works." | "Who is allowed to pay this invoice? What stops me paying *yours*?" |
| "The dashboard loads all the data." | "Whose data? Where's the tenant scope on every store query?" |
| "Usage is recorded for the subscription id I send." | "Is that subscription *mine*? Is the id from the URL trusted?" |
| "Input is validated." | "Validated for *shape*. Is it *authorized*? Different question." |

## What adding auth would take (the design)

This is a senior build project ([06/03](../06-contribution-practice/03-senior-build-projects.md)). The
shape:

1. **Authentication.** Add a scheme (JWT bearer for API clients, or cookies for the SPA). The caller's
   identity (and tenant) comes from validated claims, never from the request body.
2. **A tenant column.** Add `TenantId` to every owned entity (`Customer`, `Plan`, `Subscription`,
   `Invoice`, …). The seed and every create path set it from the authenticated principal.
3. **Scope every store query.** `Customers()` becomes `Customers(tenantId)`; the engine threads the
   tenant through. *Resource* authorization (this invoice belongs to my tenant) is checked before any
   mutation — ideally a single filter so it can't be forgotten per-endpoint.
4. **Don't trust ids from the URL/body.** Look the resource up *and* verify ownership; a 404 (not a
   403) is often the right response to hide existence.

> The reason this is hard to retrofit is exactly why it's a great lesson: authorization is a
> *cross-cutting invariant*. Bolting it on per-endpoint guarantees you'll miss one. The right move is
> a boundary (a filter, or scoping baked into the store) that's impossible to bypass.

## IDOR / authorization kata

Pick `POST /api/usage-events`. Today it trusts `SubscriptionId` from the body. Write the 4-line change
(in pseudocode, not product code) that would make it tenant-safe *assuming* auth existed. **Self-grade
— Basic:** you add an "is this subscription mine?" check. **Solid:** you put the check before the
store write and return 404 for not-mine. **Strong:** you argue it belongs in a shared filter/store
scope so the *next* endpoint can't forget it — and you name which other endpoints have the same hole.

## Verification Notes

The *absence* of auth was verified by reading every endpoint and the full DI registration
([Program.cs L7-L37](../../src/Billing.Api/Program.cs#L7-L37)) — there is no authentication/authorization
service registered. Validation anchors read-verified on 2026-06-09. Nothing executed.
