> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/04-code-reading-gym/01-annotation-drills.md.txt).

# 04.01 — Annotation Drills

For each method: read it, then annotate six things — **inputs, outputs, dependencies, invariants,
side effects, failure modes** — *before* reading the model answer. The first drill is fully worked as
a template; the rest give a key to grade against.

The annotation schema:

| Facet | The question |
| --- | --- |
| Inputs | What comes in, and what's the trusted vs untrusted split? |
| Outputs | What goes out (return + writes)? |
| Dependencies | What does it call/need to do its job? |
| Invariants | What must be true before/after? |
| Side effects | What state changes in the world? |
| Failure modes | How can it go wrong, and what happens then? |

---

## Drill 1 (worked) — `BillingEngine.GenerateInvoice`

Read [GenerateInvoice L9-L113](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L113). Then:

| Facet | Annotation |
| --- | --- |
| **Inputs** | `subscriptionId` (a `Guid`, trusted from the cycle runner), `billingDate` (the invoice date). Everything else is fetched from the store. |
| **Outputs** | Returns the new `Invoice`. Also *writes* the invoice, updated usage rows, the advanced subscription, and a queued webhook. |
| **Dependencies** | `store` (7 reads + 4 writes), `clock` (`UtcNow` for `CreatedAt`), `Money` arithmetic, `JsonSerializer` for the webhook payload. |
| **Invariants** | After it returns: an `Open` invoice exists; every usage event it counted now has `InvoiceId` set; the subscription's period has advanced by exactly one cycle; one `invoice.created` webhook is queued. Subtotal = Σ line amounts; total = max(subtotal − discount, 0). |
| **Side effects** | 4 store writes ([L82-L88, L100-L110](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L110)) — **not atomic together**. |
| **Failure modes** | Throws `KeyNotFoundException` (unknown subscription, [L13](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L13)); `InvalidOperationException` (canceled sub [L18](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L18); missing customer/plan [L24](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L24)); **`InvalidOperationException` from `Money.Add` if customer currency ≠ plan/coupon currency** (R-01); partial-write inconsistency if it crashes between the 4 writes (R-08). |

**The non-obvious bit:** the period advance ([L84-L88](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L84-L88))
uses the *old* `CurrentPeriodEnd` as the new `CurrentPeriodStart`, then computes the next end — so
periods chain without gaps, but only one step per call (R-07).

---

## Drill 2 — `IdempotencyMiddleware.InvokeAsync`

Annotate [InvokeAsync L9-L68](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L9-L68)
yourself, then check:

<details>
<summary>Key</summary>

- **Inputs:** `HttpContext` (untrusted — method, path, headers, body), the `IdempotencyStore`, options. The `Idempotency-Key` header is client-controlled.
- **Outputs:** either passes through to `next`, replays a cached response, or writes a 400/409. Side output: a cache entry on 2xx.
- **Dependencies:** `IdempotencyStore` (the cache), `IOptions<BillingOptions>` (TTL), SHA-256, the response buffering machinery.
- **Invariants:** a cached key + matching body always replays the *same* status/body; a cached key + different body always 409s; only 2xx is cached.
- **Side effects:** reads + rewinds the request body ([L31, L79](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L31-L79)); buffers + copies the response; writes the cache.
- **Failure modes:** **no key ⇒ no protection** ([L17-L21](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L17-L21)); **concurrent same-key requests both miss** (R-04); cache lost on restart; buffering the whole response in memory (a huge response = memory pressure).
</details>

**Self-grade — Basic:** you got inputs/outputs. **Solid:** you flagged "no key = no protection".
**Strong:** you found the concurrent-miss race without being told.

---

## Drill 3 — `Coupon.CalculateDiscount`

Annotate [CalculateDiscount L31-L49](../../src/Billing.Api/Domain/BillingModels.cs#L31-L49). This one
is about *invariants* and a confusing expression.

<details>
<summary>Key</summary>

- **Inputs:** `subtotal` (a `Money`), `now`. Reads the coupon's own `ExpiresAt`, `PercentOff`, `AmountOff`.
- **Outputs:** a `Money` discount, never greater than `subtotal`, never negative.
- **Dependencies:** `Money` arithmetic only — a pure domain method (no store, no clock injected; `now` is passed in).
- **Invariants:** expired coupon ⇒ zero discount ([L33-L36](../../src/Billing.Api/Domain/BillingModels.cs#L33-L36)); discount is capped at the subtotal ([L46-L48](../../src/Billing.Api/Domain/BillingModels.cs#L46-L48)).
- **Side effects:** none — pure function.
- **Failure modes:** `Money.Add` throws if `AmountOff`'s currency ≠ `subtotal`'s currency (R-01 again). The cap expression ([L46-L48](../../src/Billing.Api/Domain/BillingModels.cs#L46-L48)) evaluates `percentDiscount.Add(amountDiscount)` **twice** and is hard to read — a refactor target, not a bug.
</details>

**Drill within the drill:** rewrite the cap ([L46-L48](../../src/Billing.Api/Domain/BillingModels.cs#L46-L48))
as a single readable expression (`var d = percentDiscount.Add(amountDiscount); return d.MinorUnits > subtotal.MinorUnits ? subtotal : d;`)
and confirm it's behavior-preserving. **Self-grade — Strong:** you also note the original computes the
sum twice and that `.Max(Money.Zero(...))` is redundant since both parts are ≥ 0.

---

## Drill 4 — `WebhookSignatureVerifier.IsValid`

Annotate [IsValid L11-L40](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L11-L40).
This is the security boundary — read it like an attacker.

<details>
<summary>Key</summary>

- **Inputs:** the `HttpRequest` (untrusted headers) + the raw body string. Trusted: the configured signing secret.
- **Outputs:** `bool` — valid or not. No side effects, no logging.
- **Dependencies:** `IOptions<BillingOptions>` (secret), `HMACSHA256`, `CryptographicOperations.FixedTimeEquals`, `DateTimeOffset.UtcNow` (**directly — bypasses the clock**, [L25](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L25)).
- **Invariants:** valid ⇒ both headers present, timestamp parseable, within 5-min tolerance, and HMAC matches. The compare is **constant-time** ([L37-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37-L39)).
- **Side effects:** none.
- **Failure modes:** rejects missing/garbage headers and stale timestamps. **Does NOT prevent replay within the 5-min window** (no nonce/event-id) — feeds R-05. The direct `UtcNow` makes the tolerance untestable.
</details>

**Self-grade — Strong:** you praised `FixedTimeEquals` as the correct non-obvious choice *and* spotted
the missing replay defense *and* the clock bypass.

---

## Drill 5 — `BillingStore.TryDequeueWebhook`

Annotate [TryDequeueWebhook L229-L255](../../src/Billing.Api/Infrastructure/BillingStore.cs#L229-L255).
This is the trickiest control flow in the store.

<details>
<summary>Key</summary>

- **Inputs:** `now`. Reads `_webhookQueue` (ids) + `_webhookEvents` (rows), under the lock.
- **Outputs:** `true` + the due webhook via `out`, or `false`.
- **Dependencies:** the lock, both collections.
- **Invariants:** returns the front webhook only if its `AvailableAt ≤ now`; **skips** ids whose row no longer exists ([L237-L240](../../src/Billing.Api/Infrastructure/BillingStore.cs#L237-L240)).
- **Side effects:** dequeues (mutates the queue) on a hit or on a stale id.
- **Failure modes:** a not-yet-due front item `break`s the loop ([L249](../../src/Billing.Api/Infrastructure/BillingStore.cs#L249)) — so a delayed item *does* block items behind it (same FIFO-delay trap as payments, but the stale-id skip makes it more robust). Compare to [TryDequeuePaymentJob L200-L213](../../src/Billing.Api/Infrastructure/BillingStore.cs#L200-L213), which has no stale-id handling.
</details>

**Self-grade — Strong:** you compared it to the payment dequeue and articulated why this one is the
better-engineered of the two.

## Verification Notes

Every annotated method was read line-by-line on 2026-06-09. Keys are read-verified; failure-mode
claims tagged with an R-number cross-reference the [risk register](../08-reference/risk-register.md)
and were not executed.
