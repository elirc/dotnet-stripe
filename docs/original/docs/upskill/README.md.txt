# Upskill Training Lab

This curriculum turns the subscription billing sample into a training lab for moving from junior toward senior engineering judgment. The repo is a C#/.NET 8 minimal API plus a React/Vite client: the API registers services, CORS, idempotency middleware, hosted workers, and `/api` endpoints in [Program.cs](../../src/Billing.Api/Program.cs#L7-L37); the client renders one dashboard shell from [App.tsx](../../src/Billing.Web/src/App.tsx#L27-L395). The core domain is billing: customers, plans, coupons, subscriptions, usage, invoices, payments, and webhook events are immutable records in [BillingModels.cs](../../src/Billing.Api/Domain/BillingModels.cs#L3-L123). Money uses integer minor units in [Money.cs](../../src/Billing.Api/Domain/Money.cs#L5-L57), which is a transferable production habit. Persistence is intentionally in-memory behind [BillingStore.cs](../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L264), so persistence, transaction, EF Core, and migration lessons are taught as missing boundaries and migration exercises. Background behavior exists through hosted services registered in [Program.cs](../../src/Billing.Api/Program.cs#L19-L20) and implemented in [PaymentWorker.cs](../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L36) plus [WebhookDeliveryWorker.cs](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L6-L56). Security is partial: CORS and signed webhooks exist, but app authentication/authorization is not configured in [Program.cs](../../src/Billing.Api/Program.cs#L21-L35).

Use this over a weekend, two weeks, eight weeks, or as an ongoing code-review gym. Every codebase claim should point to a path and line anchor. Fake code is labeled `// Illustrative fake code: not from this repo`. Drills ask you to annotate inputs, outputs, dependencies, invariants, side effects, and failure modes before you change anything.

## Tracks

| Learner | Start here | Goal |
| --- | --- | --- |
| Brand-new junior | [00-fast-track.md](00-fast-track.md), then [01-codebase-cartography/02-file-reading-order.md](01-codebase-cartography/02-file-reading-order.md) | Run the app, name the parts, trace one flow. |
| Junior with stack familiarity | [01-codebase-cartography/05-key-flows.md](01-codebase-cartography/05-key-flows.md), then [04-code-reading-gym/01-annotation-drills.md](04-code-reading-gym/01-annotation-drills.md) | Make small safe changes using existing patterns. |
| Mid-level new to repo | [03-architecture-and-patterns/01-boundaries-and-layers.md](03-architecture-and-patterns/01-boundaries-and-layers.md), then [05-quality-engineering/01-testing-strategy.md](05-quality-engineering/01-testing-strategy.md) | Design cross-layer changes with tests and rollback. |
| Senior architecture review | [03-architecture-and-patterns/06-architecture-critique.md](03-architecture-and-patterns/06-architecture-critique.md), [08-reference/risk-register.md](08-reference/risk-register.md) | Identify commitments, risk owners, and migration paths. |

Senior mindset line: a junior asks “how do I make it work?”, a mid-level engineer asks “is this the right pattern?”, and a senior asks “what does this commit us to, who pays, and how do we reduce risk?”

## Map

- Orientation: [01-codebase-cartography](01-codebase-cartography/README.md)
- Stack mastery: [02-stack-and-language-mastery](02-stack-and-language-mastery/README.md)
- Architecture: [03-architecture-and-patterns](03-architecture-and-patterns/README.md)
- Reading gym: [04-code-reading-gym](04-code-reading-gym/README.md)
- Quality engineering: [05-quality-engineering](05-quality-engineering/README.md)
- Contribution practice: [06-contribution-practice](06-contribution-practice/README.md)
- Career and interviews: [07-career-and-collaboration](07-career-and-collaboration/README.md)
- Reference: [08-reference](08-reference/command-cheatsheet.md)

## Verification Notes

Inspected source files with `rg --files`; project metadata shows one API project targeting `net8.0` in [Billing.Api.csproj](../../src/Billing.Api/Billing.Api.csproj#L1-L7) and one React/Vite package in [package.json](../../src/Billing.Web/package.json#L1-L19). `dotnet`, `tsc`, and `vite` were not available locally; see [08-reference/verification-log.md](08-reference/verification-log.md).

