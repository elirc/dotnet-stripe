> Historical implementation notes. This page describes the original in-memory, unauthenticated .NET 8 version; its commands, links and planned features may differ from the current application. Use the [current verified learning course](../../astraupskill/README.md) for executable guidance. [Exact original text](../../docs/original/docs2/05-quality-engineering/05-security-checklist.md.txt).

# 05.05 — Security Checklist

Tied to the real code. The headline is harsh: this app has **no authentication and no authorization**,
so most rows below are "absent" rather than "weak". For a learning repo that's expected; the skill is
to enumerate exactly what's missing and what each fix looks like.

## The checklist, scored against this repo

| Concern | Status here | Evidence / fix |
| --- | --- | --- |
| **Authentication** | ❌ Absent | No auth middleware registered ([Program.cs L7-L37](../../src/Billing.Api/Program.cs#L7-L37)). Add JWT/cookie auth. |
| **Authorization** | ❌ Absent | No `[Authorize]`, no policies (R-02). Add a non-bypassable tenant scope. |
| **IDOR** | ❌ Present everywhere | Any id acts on any resource — [pay](../../src/Billing.Api/Program.cs#L211-L221), [usage](../../src/Billing.Api/Program.cs#L178-L202). Look up + verify ownership; 404 for not-yours. |
| **Input validation** | ⚠️ Partial | Presence checks only; no email format, no currency whitelist, no quantity bound (R-12). |
| **SQL injection** | ✅ N/A | No SQL — in-memory store. (Re-check after the EF migration: use parameters/LINQ, never string SQL.) |
| **XSS** | ✅ Low risk | React escapes by default; no `dangerouslySetInnerHTML` in [App.tsx](../../src/Billing.Web/src/App.tsx). Keep it that way. |
| **CSRF** | ⚠️ Mostly N/A | No cookies/sessions today, so no CSRF surface. **If you add cookie auth, you must add anti-forgery.** |
| **SSRF** | ✅ N/A today | Outbound webhook delivery is simulated ([worker L28-L39](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L28-L39)). **When real:** the destination is attacker-influenced (`WebhookEvent.Destination`) — validate/allowlist URLs or it's an SSRF vector. |
| **Open redirect** | ✅ N/A | No redirects. |
| **Secrets** | ⚠️ Weak | Signing secret defaults to `dev_secret_replace_me`, committed ([appsettings L4](../../src/Billing.Api/appsettings.json#L4); R-09). Move to env/secret store; fail fast in prod. |
| **Dependency risk** | ✅ Minimal | Backend has *no* NuGet packages; frontend has React/Vite/TS only. Small surface; still run `npm audit`. |
| **File uploads** | ✅ N/A | None. |
| **Webhooks (inbound)** | ✅ Good, with a gap | HMAC-SHA256 + 5-min tolerance + constant-time compare ([verifier L30-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L30-L39)). **Gap:** no `EventId` dedup → replay within window (R-05). |
| **Cookies** | ✅ N/A | None set. |
| **Rate limiting** | ❌ Absent | No throttling on any endpoint — brute-force/abuse open. Add rate limiting (esp. on the webhook + create endpoints). |
| **CORS** | ✅ Tight | Only the two `:5173` origins ([Program.cs L26](../../src/Billing.Api/Program.cs#L26)); `AllowAnyHeader/Method` but not credentials. Reasonable for dev. |
| **Error leakage** | ⚠️ Watch | Domain exception messages are returned to clients ([e.g. L81](../../src/Billing.Api/Program.cs#L81)); fine here, but don't leak internal detail in messages as the system grows. No global handler → raw 500s (R-01). |

## The one thing done genuinely well

The inbound webhook signature check is correct *crypto*: HMAC over `{timestamp}.{rawBody}`, a freshness
window to bound replay, and **`CryptographicOperations.FixedTimeEquals`**
([L37-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37-L39)) instead of a naive
`==` that would leak timing. Study this as the template for verifying signed payloads — and note the
*one* missing piece (idempotent processing of the verified event).

## Pre-merge security checklist (use on every PR)

- [ ] Does this endpoint read/mutate by id? If so, is **ownership verified** (not just existence)?
- [ ] Is new user input **validated for value**, not just presence? (format, range, allowlist)
- [ ] Any new **outbound HTTP**? Is the destination allowlisted (SSRF)?
- [ ] Any new **secret**? Is it from config/env, never committed, and validated at startup?
- [ ] Any new **cookie/session**? Did you add anti-forgery (CSRF)?
- [ ] Any new **signed/idempotent operation**? Is replay/dedup handled?
- [ ] Does an error message **leak internals**?
- [ ] Did you add a **dependency**? Is it necessary, maintained, audited?

## IDOR hunt drill

List every endpoint that takes an id (in URL or body) and acts on the resource without an ownership
check. **Self-grade — Basic:** you find `pay` and `usage-events`. **Solid:** you realize it's
*every* id-bearing endpoint, because there's no auth at all. **Strong:** you propose the single
choke-point (a tenant-scoped store, or one authorization filter) that fixes the whole class rather than
per-endpoint patches — and explain why per-endpoint patching guarantees a miss.

## Verification Notes

The absence of auth/rate-limiting was verified by reading the full DI + pipeline registration
([Program.cs L7-L37](../../src/Billing.Api/Program.cs#L7-L37)). Each ✅/⚠️/❌ is read-verified against the
anchored code on 2026-06-09. SSRF/CSRF are marked N/A *today* with the explicit condition that would
make them live — don't treat "N/A now" as "safe forever".
