# Verification Log

This log is the audit trail for the `docs2/` learning suite. It records the snapshot the
suite was written against, what was actually executed versus inferred, the provenance verdict,
and the final self-audit. **If a claim in the suite is not backed by something here or by an
opened-file anchor, treat it as inferred.**

## Snapshot

| Field | Value |
| --- | --- |
| Repository | `Subscription Billing Dashboard` |
| Pinned commit (SHA) | **None — the repository has zero commits.** `git rev-parse HEAD` fails with `unknown revision`; `git log` reports `your current branch 'main' does not have any commits yet`. |
| Snapshot basis | Working tree as read on **2026-06-09**. All line numbers in the suite refer to the files as they existed in the uncommitted working tree on that date. |
| Tooling host | Windows 11, PowerShell + bash. |
| Suite author tool | Claude Code (Opus 4.8), single-pass. |

> **Why this matters for anchors.** Normally anchors are pinned to a SHA so line numbers survive
> drift. Here there is no SHA to pin to, so anchors are pinned to a *date* and to symbol names.
> If you edit a file, re-run the self-audit script below to find anchors that have drifted.

## Provenance verdict: **Synthetic / freshly generated (AI-authored)**

Evidence gathered this session:

| Signal | Observation | Verdict weight |
| --- | --- | --- |
| Git history | `git log` → *"does not have any commits yet"*. No history at all. | Strong: synthetic |
| README self-description | [README.md](../../README.md) L16, L66: *"This workspace was generated without installing anything"*, *"did not have `dotnet`... available on PATH"*. | Strong: synthetic |
| CONTRIBUTING / CHANGELOG / issues | None present. | Synthetic |
| Code style uniformity | Uniform C# 12 primary-constructor style, uniform record usage, uniform brace style across every file. No stylistic drift between modules. | Synthetic |
| Dependency age spread | All deps are current (net8.0, React 19, Vite 6) with no legacy pins. | Synthetic |
| "Replace in production" comments | Multiple TODO-style production notes ([WebhookDeliveryWorker.cs](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L35), [architecture.md](../../docs/architecture.md)). | Synthetic (curated teaching repo) |

**Consequence (followed throughout the suite):** Modules **06 (contribution-practice)** and
**07 (career-and-collaboration)** are framed as a *simulated* OSS workflow — the learner role-plays
a contributor, the docs role-play maintainer expectations. Git-archaeology exercises are skipped.
The **architecture critique** ([../03-architecture-and-patterns/06-architecture-critique.md](../03-architecture-and-patterns/06-architecture-critique.md))
and **risk register** ([risk-register.md](risk-register.md)) are deliberately aggressive: synthetic
repos look clean but commonly hide missing auth/tenant checks, currency/coupling bugs, idempotency
races, and untested error paths. Those were hunted specifically and several were found.

### Answer-key exclusion

A search for grading/answer-key files was run:

```
find . -iname '*answer*' -o -iname '*seeded*' -o -iname '*flaw*'   →  none found
```

No `.seeded-flaws.md`, `ANSWER-KEY.md`, or equivalent exists in the tree. Nothing was excluded
on those grounds. (Had one existed, only its path would be noted here and its contents left unopened.)

## Repo sizing → curriculum tier

| Measure | Value |
| --- | --- |
| Projects in solution | 1 in `.sln` ([Billing.Api](../../SubscriptionBillingDashboard.sln#L5)); the React app `Billing.Web` exists on disk but is **not** in the solution. |
| Deployable units | 2 (API + SPA). |
| C# LOC | 1,483 (`find src -name '*.cs' | xargs wc -l`). |
| TS/TSX LOC | 604. |
| **Tier** | **Small** (≤3 projects, <10k LOC). Target counts taken from the Small column. |

## Commands run this session

Legend: ✅ run with real output captured · ⚠️ run, environment-limited · ❌ documented but **not** run here (marked *inferred* wherever it appears in the suite).

| # | Command | Result |
| --- | --- | --- |
| 1 | `git rev-parse HEAD` | ✅ `fatal: ambiguous argument 'HEAD': unknown revision` — confirms no commits. |
| 2 | `git log --oneline` | ✅ `fatal: your current branch 'main' does not have any commits yet`. |
| 3 | `git status --short` | ✅ Only `docs/`-area files staged/untracked; `src/` is untracked working tree. |
| 4 | `dotnet --version` | ✅ output: `dotnet: command not found`. **The .NET SDK is not installed on this host.** |
| 5 | `node --version` | ✅ `v22.16.0`. |
| 6 | `npm --version` | ✅ `10.9.2`. |
| 7 | `find src -name '*.cs' | xargs wc -l` | ✅ `1483 total`. |
| 8 | `find src -name '*.ts' -o -name '*.tsx' | xargs wc -l` | ✅ `604 total`. |
| 9 | `npm install` (in `src/Billing.Web`) | ⚠️ See "Frontend verification" below. |
| 10 | `npx tsc --noEmit` (in `src/Billing.Web`) | ⚠️ See "Frontend verification" below. |

### Consequence for the command cheatsheet

Because **`dotnet` is not on this host**, every `dotnet build`/`dotnet run`/`dotnet test` command in
the suite is labeled **inferred** — it is the correct command per [Billing.Api.csproj](../../src/Billing.Api/Billing.Api.csproj#L3)
(`net8.0`) and [launchSettings.json](../../src/Billing.Api/Properties/launchSettings.json#L8), but it
was not executed here. Run it yourself after installing the .NET 8 SDK; that is the learner's first
"verify, don't trust" exercise. See [command-cheatsheet.md](command-cheatsheet.md).

### Frontend verification

`npm install` and `npx tsc --noEmit` were attempted in `src/Billing.Web`. **Both failed for
environment reasons, not code reasons:**

```
npm warn tar TAR_ENTRY_ERROR ENOSPC: no space left on device, write
npm error ... ENOSPC
```

`npm install` aborted with **ENOSPC (no space left on device)** mid-extraction, so `node_modules`
was incomplete and `npx tsc` could not find a real TypeScript compiler (it tried to fetch a bogus
`tsc@2.0.4` shim). The partial `node_modules` and `package-lock.json` were then removed to leave the
tree clean. **Net result: the frontend was NOT type-checked or built on this host.** Every
`npm install` / `npm run build` / `tsc` command in the suite is therefore labeled **inferred**.
The commands themselves are correct per [package.json](../../src/Billing.Web/package.json#L6-L10)
(`"build": "tsc -b && vite build"`); run them yourself once you have working disk + network.

**Bottom line: nothing in this repository — backend or frontend — was compiled or executed during
authoring.** All behavioral statements are read-verified or hand-traced from source, never observed
at runtime. This is the single most important caveat in the suite.

## Verification approach used for behavioral claims

The .NET runtime is unavailable, so **no C# code in this repo was executed** during authoring.
Every behavioral claim about the backend is one of:

1. **Read-verified** — derived by reading the opened source file; anchored to symbol + line range.
2. **Hand-traced** — computed by hand from the code (e.g., the worked invoice total in
   [../01-codebase-cartography/05-key-flows.md](../01-codebase-cartography/05-key-flows.md)); labeled as such.
3. **Inferred** — anything depending on runtime behavior that was not executed; labeled *inferred*.

No claim is labeled "bug" without a code path that demonstrates it; uncertain items are labeled
*investigate* / *possible risk* in the [risk-register.md](risk-register.md).

## Self-audit results

Run at the end of authoring, 2026-06-09.

### (1) Anchor re-verification — 10 anchors re-opened, all correct

| # | Anchor | Re-read line range | Result |
| --- | --- | --- | --- |
| 1 | `Money.Multiply` | [Money.cs L43-L47](../../src/Billing.Api/Domain/Money.cs#L43-L47) | ✅ `MidpointRounding.AwayFromZero` at L45 confirmed |
| 2 | `RunBillingCycle` | [BillingEngine.cs L115-L127](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L127) | ✅ Active/Trialing filter L118, period check L120 |
| 3 | MRR aggregate (R-01) | [Program.cs L262-L274](../../src/Billing.Api/Program.cs#L262-L274) | ✅ `Money.Zero("USD")` seed L262, `sum.Add` L273 |
| 4 | Idempotency 409 | [IdempotencyMiddleware.cs L37-L42](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L37-L42) | ✅ hash mismatch → 409 |
| 5 | `TryDequeueWebhook` | [BillingStore.cs L229-L255](../../src/Billing.Api/Infrastructure/BillingStore.cs#L229-L255) | ✅ stale-id skip L236-L240, `break` L249 |
| 6 | `FixedTimeEquals` | [WebhookSignatureVerifier.cs L37-L39](../../src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs#L37-L39) | ✅ constant-time compare |
| 7 | default-selection effect | [App.tsx L57-L62](../../src/Billing.Web/src/App.tsx#L57-L62) | ✅ updater form avoids stale closure |
| 8 | webhook retry/backoff | [WebhookDeliveryWorker.cs L41-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L41-L53) | ✅ exp backoff `Math.Pow(2,…)` L48, `LastError = ex.Message` |
| 9 | seed `subA` + usage | [BillingStore.cs L305](../../src/Billing.Api/Infrastructure/BillingStore.cs#L305), [L316-L320](../../src/Billing.Api/Infrastructure/BillingStore.cs#L316-L320) | ✅ GUID `062a6a8f-…` + 8 events `18000+i*2500` (Flow 2 / test recipes) |
| 10 | `Coupon.CalculateDiscount` | [BillingModels.cs L31-L49](../../src/Billing.Api/Domain/BillingModels.cs#L31-L49) | ✅ expiry guard + subtotal cap |

**Result: 10/10 anchors correct. No fixes required.** (Line numbers cannot have drifted — no source
file was modified during authoring; the suite is documentation-only.)

### (2) Relative-link check — 0 real broken links

A script (`/tmp/linkcheck.mjs`, run via `node`; not part of the product tree) walked all 47 files,
extracted every `](…)` relative link, stripped `#Lx-Ly` anchors, resolved against each file's directory,
and tested existence:

```
Checked 884 relative links across 47 files. Broken: 1
BROKEN: docs2\README.md -> relative/path#Lx-Ly
```

The single "broken" hit is a **false positive**: it's the literal anchor-format *example*
`[SymbolName](relative/path#Lx-Ly)` in the README's conventions table, not an actual link. **Real
broken links: 0/884.**

> Note: this checker validates that link *paths* (files) exist; it does not validate that a `#Lx-Ly`
> fragment points at the intended symbol. The 10-anchor sample above covers that dimension by hand.

## Unfilled / thin stubs

**None.** All 47 files in the planned tree were written with full content — no file was left as a
`TODO` stub. The full tree:

- Root: `README.md`, `00-fast-track.md`
- `01-codebase-cartography/`: README + 5 (system-map, file-reading-order, domain-glossary,
  runtime-and-tooling-map, key-flows)
- `02-stack-and-language-mastery/`: README + 4
- `03-architecture-and-patterns/`: README + 7 (incl. `07-comparative-implementations.md`)
- `04-code-reading-gym/`: README + 4
- `05-quality-engineering/`: README + 6
- `06-contribution-practice/`: README + 4
- `07-career-and-collaboration/`: README + 4 (incl. `04-interview-prep-from-this-repo.md`)
- `08-reference/`: command-cheatsheet, risk-register, learning-rubrics, verification-log

**Deliberately lean (by design, not omission):**
- `02-*` runtime/framework pages teach standard CLR/React concepts *through* the repo's anchors; the
  general-concept prose is intentionally compact since it's not repo-specific.
- Performance ([05/04](04-performance-thinking.md)) presents *read-identified* hotspots only — no
  measurements were possible (no runtime), and this is stated in-page.

**Counts delivered (Small tier targets in parentheses):** key flows 4 (3–4) · pattern cards 9 (6–10) ·
comparison cards 3 (3) · annotation drills 5 (4–6) · trace tables 4 (3) · fake-code contrasts 6 (4–6) ·
review katas 5 (4–6) · good-first tickets 8 (6–10) · mid-level tickets 5 (4–6) · senior projects 3 (2–3)
· debugging scenarios 4 (3) · security checklist 1 · performance guide 1 · PR/RFC guide 1 · interview-prep
1 · verification log 1.

## Open uncertainties (carry into your own investigation)

| Item | Why uncertain | How to resolve |
| --- | --- | --- |
| Does the dashboard actually 500 when a non-USD plan has an active subscription? | Read-verified currency-mismatch path in [Program.cs CalculateMetrics](../../src/Billing.Api/Program.cs#L262-L274), but not executed. | Install .NET 8, create a EUR plan + active subscription, GET `/api/dashboard`. |
| Is the idempotency double-execution window reachable in practice? | The TryGet→Set gap in [IdempotencyMiddleware](../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L35-L66) is real in code; reachability depends on concurrency. | Fire two concurrent identical POSTs with the same `Idempotency-Key`. |
| Whether the webhook retry/backoff path is truly dead | The `try` body in [WebhookDeliveryWorker.Deliver](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L26-L54) has no throwing statement under normal operation. | Inject a fault into `SaveWebhook` and observe. |
