# Verification and practical limits

This file separates implemented behavior from proposed exercises and records how the delivery was checked. The accompanying `evidence.json` is generated only after the final checks pass. The collection-level delivery manifest also records each applied file's before/after hash. Historical original notes claiming the project was never compiled describe the preserved earlier version, not this implementation.

## Backend and HTTP evidence

The final broad .NET run passed **66 tests**. The suite includes the [money tests](../tests/Billing.Tests/MoneyTests.cs), [workflow tests](../tests/Billing.Tests/WorkflowTests.cs) and [HTTP tests](../tests/Billing.Tests/HttpTests.cs). Earlier runs exposed the original missing namespace and nullable-money compile errors, frontend type support gaps, and an outdated transitive SQLite package. Those issues were corrected. A test-data type mismatch in four coupon cases was corrected before the full passing run; it was not hidden by excluding those cases.

Verified behaviors include exact money JSON round trips, currency and amount bounds, SQLite restart persistence, rollback, unknown-file refusal, relationship isolation, case-insensitive uniqueness, stale edits, immutable referenced pricing, usage correction before billing, usage locking after billing, bounded catch-up, anchored monthly dates, cancellation, duplicate invoice-period prevention, payment retry identity, terminal payment protection, receipt replay, concurrent same-key requests, audit rollback, current membership permissions, expired sessions, HTTP preconditions, strict JSON, body limits, local-origin checks, pagination, logout, currency-grouped totals and signed event deduplication.

The HTTP host injects a fresh temporary database before the application resolves its database service. Tests do not open or mutate a normal user workspace. No real card, provider API, external receiver or email system is involved.

## Browser, package and dependency checks

[browser_smoke.py](../tests/browser_smoke.py) exercises the published React application against a real loopback ASP.NET process. It creates its own temporary database and stops that process afterward. It checks forms, a stale draft, a response deliberately lost after commit, exact retry, confirmed deletion, catalog relationships, usage, invoice arithmetic, failed and successful payment attempts, simulated outbox state, pagination, search, Viewer controls and narrow-screen overflow. The final results and reviewed images are included in the delivery evidence.

The final browser run passed **15 workflow checks** with no uncaught JavaScript errors. Three additional package checks confirmed matching browser assets, exclusion of application data and the presence of dependency lock files. Together with the 66 .NET cases, this is **84 recorded checks**, without adding repeated runs to the total. An earlier browser run exposed ambiguous select-control names; explicit accessible names were added before rebuilding and passing the full workflow.

Read the [machine-readable evidence](evidence.json) and reviewed [desktop](images/desktop.png) and [narrow](images/narrow.png) screenshots.

The local package is built through [publish-local.ps1](../scripts/publish-local.ps1), which installs the locked frontend tree, runs TypeScript and Vite, copies browser assets and publishes the .NET application. Application data is excluded from publish content. The frontend and .NET dependency trees are pinned by package manifests and lock files. Dependency audits describe the registry information available at verification time; they do not guarantee that no future advisory will appear.

## Boundaries you should retain

This is a loopback-only, single-node SQLite learning application. A workspace is capped at 5,000 domain records; list pages are SQL-paginated, but some internal workflows materialize bounded collections. The schema stores immutable JSON payloads inside relational identity and relationship tables. It is not a full normalized accounting ledger or a scalable multi-node billing service.

Only USD, EUR and GBP are supported. There are no taxes, proration, refunds, credit notes, exchange rates, automatic billing scheduling, account recovery, password-change workflow, public identity integration or real webhook delivery. New membership creation uses a new global email; linking an already existing account to another workspace is not implemented. The founding Owner membership is protected. Audit history and provider event history have no retention UI.

Operation receipts expire after 24 hours. Browser retry state lasts only while its tab stays open. Registration and login are outside the durable business-operation receipt protocol. If registration is interrupted after commit, try signing in before creating another workspace. Outbound events are explicitly marked Simulated; no claim of real delivery or exactly-once external processing is made.

The original project had in-memory state, and no original database was opened or replaced. Existing unknown database files require a separate reviewed migration or a fresh learning path. Exact original text is available in [the snapshot index](../docs/original/README.md). The exercises in [Practice](05-PRACTICE.md) remain proposed work and are not included in the implemented-feature claims.
