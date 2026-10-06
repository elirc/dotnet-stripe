# Repo-to-Training-Lab: Subscription Billing Dashboard

A self-study curriculum that turns **this specific repository** into a training lab. The goal is to
move you from "I can read the code" to "I can own a billing subsystem": run it, trace a feature end
to end, make safe changes, design cross-layer features, debug methodically, critique the
architecture, and defend design trade-offs in a mid-level interview.

It teaches two things at once:

- **This codebase** — where invoicing lives, how idempotency is wired, why money is an integer.
- **Transferable judgment** — what each pattern commits a team to, how it fails, how you'd spot the
  same shape in a different repo.

> **Read this first:** nothing in this repo was compiled or run while the suite was written — the
> `.NET` SDK is not installed on the authoring host and `npm install` hit a disk limit. Every
> behavioral claim is *read-verified from source* or *hand-traced*, never observed at runtime. Your
> very first exercise is to run it yourself and confirm. See
> [08-reference/verification-log.md](08-reference/verification-log.md).

> **Status (re-checked 2026-10-06 by reading source; nothing run):** two facts change how you use this suite.
> 1. **`main` does not compile**, so "run it yourself and confirm" fails at step one. There are two one-line
>    errors: CS0246 in `src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs` (it uses `IBillingClock` at line 90
>    without `using Billing.Api.Domain;`) and CS0173 at `src/Billing.Api/Program.cs:143` (`cond ? null : Money...`,
>    where `Money` is a non-nullable struct; write `(Money?)null`). Make both edits on a scratch branch before any
>    `dotnet run` in this suite. `learn/README.md` Session 1 turns this into an exercise, and
>    `training/_answers/review.md` finding 0 documents it.
> 2. The repo now has git history and two pushed training branches (`origin/training/incidents/001-*`, `002-*`), so
>    the "zero git commits" provenance below is out of date. There are still **no tests and no CI workflow**.
>
> Where this suite and `training/_answers/review.md` disagree, the review was written later and with the compile
> errors in view.

## Repo identity (the 6-sentence version)

The **Subscription Billing Dashboard** is a Stripe/Chargebee-style billing backend plus a React
operator dashboard, built to *teach* enterprise billing fundamentals rather than to ship. The
backend ([src/Billing.Api](../src/Billing.Api)) is a dependency-free **.NET 8 minimal API** with an
in-memory store, idempotent write APIs, two background workers (payment processing and outbound
webhook delivery), an HMAC-signed inbound webhook endpoint, and a usage-based billing simulator.
The frontend ([src/Billing.Web](../src/Billing.Web)) is a single **React 19 + Vite** page that reads
one fat `/api/dashboard` payload and posts forms back. Money is modeled as **integer minor units**
([Money.cs](../src/Billing.Api/Domain/Money.cs)) to avoid floating-point drift, and the domain is
expressed as immutable C# `record` types. **Provenance: this is a freshly generated, synthetic
teaching repo** — zero git commits, no contributors, uniform style — so modules 06–07 are run as a
*simulated* OSS workflow and the critique is deliberately aggressive. The codebase is deliberately
"clean on the surface but load-bearing underneath": several latent bugs (currency coupling,
idempotency races, a dead retry path, zero auth) are left in for you to find.

## Who this is for

A junior engineer aiming for mid-level, then senior, who is comfortable enough to read code but
wants to build *judgment*. The four-stage arc:

| Stage | The question you're learning to answer | Where you'll spend time |
| --- | --- | --- |
| **Orientation** | "How do I run it and follow one feature end to end?" | [00-fast-track.md](00-fast-track.md), [01-codebase-cartography](01-codebase-cartography/) |
| **Junior** | "How do I make a small change using the existing pattern?" | [06/01-good-first-tickets.md](06-contribution-practice/01-good-first-tickets.md), [04-code-reading-gym](04-code-reading-gym/) |
| **Mid-level** | "Is this the *right* pattern? Where are the tests? Why did it break?" | [05-quality-engineering](05-quality-engineering/), [06/02-mid-level-feature-tickets.md](06-contribution-practice/02-mid-level-feature-tickets.md) |
| **Senior** | "What does this design commit us to, who pays for it, how do we reduce risk?" | [03/06-architecture-critique.md](03-architecture-and-patterns/06-architecture-critique.md), [03/07-comparative-implementations.md](03-architecture-and-patterns/07-comparative-implementations.md), [08/risk-register.md](08-reference/risk-register.md) |

**The one-line mindset ladder this suite trains:**
> A **junior** asks *"how do I make it work?"* — a **mid-level** asks *"is this the right pattern?"* —
> a **senior** asks *"what does this commit us to, who pays, and how do we reduce the risk?"*

## How to use it over time

| You have… | Do this |
| --- | --- |
| **A weekend** | [00-fast-track.md](00-fast-track.md) end to end → trace the *metered billing* flow in [01/05-key-flows.md](01-codebase-cartography/05-key-flows.md) → read one comparison card in [03/07](03-architecture-and-patterns/07-comparative-implementations.md) → do Good-First Ticket #1. |
| **Two weeks** | Modules 01 → 02 → 03 in order, one annotation drill ([04/01](04-code-reading-gym/01-annotation-drills.md)) per day, finish 2–3 good-first tickets. End each day in the [risk-register](08-reference/risk-register.md). |
| **Eight weeks** | All modules in order. Weeks 1–2 orient + junior tickets; 3–4 quality engineering + write real tests; 5–6 mid-level tickets + debugging scenarios; 7–8 a senior build project + the full critique + interview prep. |
| **Ongoing** | Use [04-code-reading-gym](04-code-reading-gym/) and [07/04-interview-prep](07-career-and-collaboration/04-interview-prep-from-this-repo.md) as a gym. Re-grade yourself against [08/learning-rubrics.md](08-reference/learning-rubrics.md) monthly. |

## Recommended paths by starting point

- **Brand-new junior (new to C#/React):** 00 → 02/01 (runtime) → 02/02 (frameworks) → 01/05 (one flow) → 04/01 (annotate one file) → 06/01 (one ticket). Skip the critique until you can run it.
- **Junior fluent in the stack:** 00 → 01/05 (all flows) → 03/05 (pattern catalog) → 06/01–02 tickets → 04/04 (review katas).
- **Mid-level new to the repo:** 01/01 (system map) → 03/01–04 (boundaries, data, auth, reliability) → 05/03 (debugging) → 08/risk-register → 06/02 tickets.
- **Senior doing architecture review:** 03/06 (critique) → 03/07 (comparisons) → 08/risk-register → 06/03 (build projects) → 07/04 (interview prep as a checklist of what you should be able to defend).

## Conventions used everywhere in this suite

| Convention | Rule |
| --- | --- |
| **Anchors** | A claim about this codebase is anchored as `[SymbolName](relative/path#Lx-Ly)` — symbol name plus line range. If something has no anchor, it is general knowledge or explicitly labeled inferred. |
| **Snapshot** | Line numbers refer to the working tree of **2026-06-09**. There is no commit to pin to (the repo has zero commits) — see the [verification-log](08-reference/verification-log.md). Re-run the audit script there if files drift. |
| **Real vs illustrative** | Code copied/derived from this repo is anchored. Invented code is fenced and tagged `// Illustrative: not from this repo`. The two are never mixed silently. |
| **Verified vs inferred** | Commands actually run on the authoring host are marked ✅ in the [verification-log](08-reference/verification-log.md). Everything depending on a runtime we didn't have (all `dotnet`/`vite` execution) is marked **inferred**. |
| **Drills & self-grading** | Most pages end in a drill with a **Basic / Solid / Strong** rubric. Do them on paper or a scratch branch — do not edit product code to "answer" them. |
| **Verification Notes** | Key docs carry a short *Verification Notes* block stating what was read-verified vs hand-traced vs inferred. |

## Map of the suite

| Module | What it builds |
| --- | --- |
| [00-fast-track.md](00-fast-track.md) | A weekend "first win": run, trace, one safe change. |
| [01-codebase-cartography](01-codebase-cartography/) | System map, reading order, glossary, tooling, end-to-end flows. |
| [02-stack-and-language-mastery](02-stack-and-language-mastery/) | CLR/GC/async, ASP.NET pipeline + DI, React render model, the type system, the build. |
| [03-architecture-and-patterns](03-architecture-and-patterns/) | Boundaries, persistence, auth, reliability, pattern catalog, **critique**, **comparative implementations**. |
| [04-code-reading-gym](04-code-reading-gym/) | Annotation drills, trace tables, fake-code contrasts, review katas. |
| [05-quality-engineering](05-quality-engineering/) | Testing strategy, writing tests here, debugging, performance, security, observability. |
| [06-contribution-practice](06-contribution-practice/) | Good-first / mid-level / senior tickets + refactor katas (as a *simulated* OSS flow). |
| [07-career-and-collaboration](07-career-and-collaboration/) | Review mindset, PRs/RFCs, maintainer comms, **interview prep from this repo**. |
| [08-reference](08-reference/) | Command cheatsheet, **risk register**, learning rubrics, **verification log**. |

## Suggested first session

1. [00-fast-track.md](00-fast-track.md) — install, run, and confirm one endpoint returns data.
2. [01/05-key-flows.md](01-codebase-cartography/05-key-flows.md) — trace **Flow 2: Metered billing cycle** (it has a fully hand-worked invoice total you can check).
3. [03/07-comparative-implementations.md](03-architecture-and-patterns/07-comparative-implementations.md) — read **Comparison 1: in-memory store vs EF Core** and do the first half of its migration drill.

That session alone takes you from "never seen it" to "I can explain why the store is the most
important boundary in the repo" — which is exactly the senior framing.
