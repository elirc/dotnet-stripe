# Compare template — Blind Code Review

Fill this in after you've written your own findings in a scratch file (not in this repo's
`training/` — keep your working notes separate so you don't contaminate the exercise for
next time) and only then opened `training/_answers/review.md`.

## 1. Coverage
| # | My finding (yes/no/partial) | Their severity vs mine | Notes |
|---|---|---|---|
| 1 (duplicate invoice race) | | | |
| 2 (webhook EventId not deduped) | | | |
| 3 (PaymentWorker vs real webhook race) | | | |
| 4 (fake webhook delivery) | | | |
| 5 (no auth) | | | |
| 6 (non-durable queues/idempotency) | | | |
| 7 (idempotency key not tenant-scoped) | | | |
| 8 (failed requests not cached) | | | |
| 9 (hardcoded secret default) | | | |
| 10 (clock bypass in signature verifier) | | | |
| 11 (CalculateDiscount fragile duplication) | | | |
| 12 (metered usage pools all metrics) | | | |
| 13 (cancellation unimplemented) | | | |
| 14 (invoice numbers not sequential) | | | |
| 15 (no dead-letter path) | | | |
| 16 (no correlation id) | | | |
| 17 (zero tests despite testing-strategy docs) | | | |
| 18 (Billing.Web not in .sln) | | | |

## 2. What I missed entirely
List every finding above you marked "no." For each, write one sentence: *why* didn't I
see it — did I not read that file closely enough, did I not trace the concurrency path,
did I assume the happy path, did I not check what the background workers actually do vs.
what their names imply?

## 3. What I flagged that they didn't
If you found something real that isn't in `review.md`, it's not automatically wrong —
sanity-check it against the evidence bar (file:line, traceable, no speculation) and note
whether it should be added.

## 4. The pattern behind the misses
After filling in sections 2 and 3, write 2-3 sentences: what *category* of bug do you
consistently under-weight (concurrency? fake integrations that look complete? missing
auth because the happy-path demo never needed it?) — this is the thing to deliberately
practice next, not just "read more carefully."
