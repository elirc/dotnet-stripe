# Original project snapshot

The 125 original text files were copied byte-for-byte before implementation changes. Their relative paths are preserved and `.txt` is appended, so old source is available for comparison without being compiled.

Examples: [original README](README.md.txt), [original API](src/Billing.Api/Program.cs.txt), [original billing engine](src/Billing.Api/Infrastructure/BillingEngine.cs.txt), [original React screen](src/Billing.Web/src/App.tsx.txt).

These files describe an older .NET 8, in-memory, unauthenticated simulator. They are historical evidence, not current setup instructions. Start with the [current learning course](../../astraupskill/README.md).

No application database, dependency directory, live credentials or build output was included. The original configuration contained only the documented placeholder webhook secret. The delivered application generates a private secret per workspace.
