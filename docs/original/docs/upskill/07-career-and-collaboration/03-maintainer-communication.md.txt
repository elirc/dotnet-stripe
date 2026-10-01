# Maintainer Communication

## Asking A Question

Bad: “How do I add auth?”  
Better: “I see no auth registration in [Program.cs](../../../src/Billing.Api/Program.cs#L9-L35). I’m considering a dev API-key policy first, then tenant checks around invoice pay [Program.cs](../../../src/Billing.Api/Program.cs#L211-L221). Is that direction acceptable for this learning repo?”

## Bug Report

Include reproduction, expected, actual, environment, anchors, and smallest failing path. Example: “Running a billing cycle with a due subscription reaches [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L115-L124), but invoice line totals from [BillingEngine.cs](../../../src/Billing.Api/Infrastructure/BillingEngine.cs#L44-L64) are unexpected.”

## Responding To Review

Use: “Good catch. I moved the invariant into [BillingStore.cs](../../../src/Billing.Api/Infrastructure/BillingStore.cs#L106-L119) and added a test plan around [Program.cs](../../../src/Billing.Api/Program.cs#L155-L174).” Avoid arguing from preference when the reviewer is pointing at a boundary or risk.

## Respectful Disagreement

Bring evidence: “I agree the endpoint is long, but for this ticket I’d keep validation in [Program.cs](../../../src/Billing.Api/Program.cs#L87-L120) and open a follow-up to extract plan creation policy after tests exist.”

