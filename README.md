# Subscription Billing Workshop

A C#/.NET 10, SQLite and React application for learning reliable CRUD web development through subscription billing. Customers, plans, coupons, subscriptions, usage, invoices and payment attempts form one connected domain.

**Local simulator:** no real card charges, emails or outbound HTTP webhooks. The custom signed callback lab is not Stripe-compatible.

## Start here

Read the [detailed learning course](astraupskill/README.md). It contains nine guides with code maps, worked changes, failure traces, exercises and review criteria.

Requirements: .NET 10 SDK and Node 22.16 or later in the Node 22 line. SDK selection and dependency lock files are included.

```powershell
# Terminal 1, from the project root:
./scripts/run-api.ps1

# Terminal 2:
cd src/Billing.Web
npm.cmd ci --ignore-scripts --no-audit --no-fund
npm.cmd run dev
```

Open http://127.0.0.1:5173 and create a workspace using fictional data. There is no default password. Sign in with email, password and workspace code. Sessions stay in browser memory and expire after eight hours; reloading requires sign-in.

The default database is `src/Billing.Api/App_Data/billing-learning.sqlite`. Set `Billing__Database` to a new path for an isolated lab. Existing unknown database files are refused, never automatically reset or migrated. Stop the app before making a consistent local backup, and keep the database private.

## What is implemented

- Workspace ownership, Operator/Viewer memberships, password derivation, revocable sessions and server-side authorization.
- Customer, plan and coupon create/read/update/delete workflows with search, pagination, archive states and protected references.
- Subscription creation, period-end cancellation/resumption before cancellation, and deletion when unused.
- Usage create/read/update/delete before invoicing; immutable invoice snapshots and explicit voiding rules.
- Integer money for USD/EUR/GBP, currency-grouped totals, anchored monthly/annual billing and bounded catch-up cycles.
- SQLite transactions, composite relationship foreign keys, unique indexes and optimistic concurrency through strong ETags.
- Durable 24-hour operation receipts: retries replay the committed response; changed requests cannot reuse a key.
- Durable payment attempts and outbox events, explicit success/decline simulation, and signed callback deduplication.
- React forms that retain stale drafts and retain an uncertain operation for an exact retry while the tab stays open.
- Backend, HTTP and browser verification. See [evidence and limitations](astraupskill/VERIFICATION.md).

## Validate and package

```powershell
./scripts/check.ps1
./scripts/publish-local.ps1
cd .artifacts/publish
dotnet Billing.Api.dll --urls http://127.0.0.1:5088
```

The package serves the compiled React app and API on one origin. It excludes application data and does not deploy anything.

Optional browser suite, from the project root after publishing:

```powershell
python -m pip install -r tests/requirements-browser.txt
python -m playwright install chromium
python tests/browser_smoke.py --output .artifacts/browser-run-01
```

Use a new output directory for each run. The browser suite starts its own loopback server with a temporary database and stops it afterward. `ASTRA_BROWSER_EXECUTABLE` can select an already installed compatible Chromium.

## Learning scope

This is a single-node workshop capped at 5,000 domain records per workspace. It has no taxes, proration, refunds, credit notes, real payment provider, production identity service, account recovery, automatic billing scheduler or real webhook sender. The storage layer is a relational envelope around immutable JSON domain records; some workflows materialize bounded collections. These tradeoffs are discussed in the course and proposed exercises are labeled separately from implemented features.

The original .NET 8/in-memory implementation and documentation were preserved byte-for-byte as text under [docs/original](docs/original/README.md). Existing `docs/upskill` and `docs2` pages are historical reading. Current executable guidance is in `astraupskill`.
