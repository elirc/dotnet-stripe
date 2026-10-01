# Codebase Cartography

Use this section to build the map before touching behavior. The API composition lives in [Program.cs](../../../src/Billing.Api/Program.cs#L7-L37), the domain records in [BillingModels.cs](../../../src/Billing.Api/Domain/BillingModels.cs#L3-L123), the in-memory store in [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L5-L264), and the React app in [App.tsx](../../../src/Billing.Web/src/App.tsx#L27-L395).

Read in this order:

1. [01-system-map.md](01-system-map.md)
2. [02-file-reading-order.md](02-file-reading-order.md)
3. [03-domain-glossary.md](03-domain-glossary.md)
4. [04-runtime-and-tooling-map.md](04-runtime-and-tooling-map.md)
5. [05-key-flows.md](05-key-flows.md)

Verification note: no controllers, EF Core migrations, auth handlers, or test projects were found in `rg --files`; the API uses minimal endpoint mapping in [Program.cs](../../../src/Billing.Api/Program.cs#L39-L250).

