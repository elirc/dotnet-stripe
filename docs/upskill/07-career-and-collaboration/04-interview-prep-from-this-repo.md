# Interview Prep From This Repo

Use this repo to practice mid-level interviews with concrete examples.

## C#/.NET Runtime

**Question:** Why use a value object for money?  
Junior: “So money is formatted consistently.”  
Mid-level: “`Money` stores integer minor units and currency in [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L5-L57), avoiding floating-point drift and centralizing currency checks.”  
Senior: “It also defines a domain invariant and migration commitment. Sub-cent usage pricing would need a deliberate type change, because current `FromMajor` rounds to cents in [Money.cs](../../../src/Billing.Api/Domain/Money.cs#L12-L16).”

**Question:** Explain singleton lifetime risk.  
Junior: “Singleton means one instance.”  
Mid-level: “`BillingStore` is singleton in [Program.cs](../../../src/Billing.Api/Program.cs#L14-L18), so all requests/workers share it.”  
Senior: “If we replace it with EF, injecting scoped `DbContext` into singleton workers like [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L6-L11) creates a captive dependency; use scopes per job.”

## ASP.NET/Core API

**Question:** How does middleware differ from endpoint logic?  
Junior: “Middleware runs first.”  
Mid-level: “CORS and idempotency are middleware in [Program.cs](../../../src/Billing.Api/Program.cs#L32-L35); endpoints handle specific routes in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L250).”  
Senior: “Cross-cutting replay behavior belongs in middleware, but its cache key in [IdempotencyMiddleware.cs](../../../src/Billing.Api/Infrastructure/IdempotencyMiddleware.cs#L40-L45) must later include tenant/user context once auth exists.”

**Question:** Where is validation?  
Junior: “In the endpoint.”  
Mid-level: “Endpoint validation handles shape in [Program.cs](../../../src/Billing.Api/Program.cs#L67-L202); store protects invariants like duplicate email in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L64-L76).”  
Senior: “That split is good only if ownership is explicit; otherwise rules fall between boundaries.”

## React

**Question:** How does the dashboard fetch data?  
Junior: “`useEffect` calls the API.”  
Mid-level: “Initial effect calls `refresh` in [App.tsx](../../../src/Billing.Web/src/App.tsx#L53-L71), which calls `getDashboard` in [api.ts](../../../src/Billing.Web/src/api.ts#L145-L146).”  
Senior: “I’d discuss cancellation/stale response risk because `refresh` has no abort controller in [App.tsx](../../../src/Billing.Web/src/App.tsx#L64-L71).”

**Question:** Why use `useMemo` here?  
Junior: “For performance.”  
Mid-level: “Lookup maps are derived from dashboard arrays in [App.tsx](../../../src/Billing.Web/src/App.tsx#L49-L51).”  
Senior: “It is useful but not a silver bullet; correctness depends on dependency arrays and stable data ownership.”

## Debugging

**Question:** Payment never marks paid. How debug?  
Junior: “Check the worker.”  
Mid-level: “Trace pay endpoint [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221), queueing [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L152-L153), dequeue [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L200-L213), worker [PaymentWorker.cs](../../../src/Billing.Api/Infrastructure/PaymentWorker.cs#L20-L35).”  
Senior: “Also ask what signal would reveal pending job age and whether queue durability is acceptable.”

## System Design

**Question:** Make this production-ready.  
Junior: “Add a database and login.”  
Mid-level: “Replace [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L17) with DB-backed persistence, add auth to [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35), and tests around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L9-L204).”  
Senior: “Prioritize tenant authorization, transactional invoice generation around [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L82-L105), durable outbox for [WebhookEvent](../../../src/Billing.Api/Domain/BillingModels.cs#L112-L123), observability, and rollback.”

## Code Review

**Question:** A PR adds a new invoice status. What do you check?  
Junior: “The enum compiles.”  
Mid-level: “Update [BillingEnums.cs](../../../src/Billing.Api/Domain/BillingEnums.cs#L24-L31), [api.ts](../../../src/Billing.Web/src/api.ts#L5-L5), and [styles.css](../../../src/Billing.Web/src/styles.css#L198-L222).”  
Senior: “Also check state-transition rules in [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L129-L199), migrations, tests, and how old clients behave.”

## Behavioral Prompts

**Prompt:** Tell me about a time you reduced risk.  
Junior answer: “I added validation.”  
Mid-level answer: “I added server validation around a billing endpoint like [Program.cs](../../../src/Billing.Api/Program.cs#L178-L202) and a regression test.”  
Senior answer: “I identified the risk boundary, measured blast radius, added tests, documented rollback, and avoided mixing it with unrelated refactors.”

