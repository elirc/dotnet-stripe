# Training materials — SubscriptionBillingDashboard (.netstripe)

**Tier: DRILL.** This project gets a blind code review, two planted incidents, a
characterization-tests exercise, and a small interview kit (no ladder/navigation/agentic
phases — those are LAB-tier only).

Batch: B01. Prior audit: `Desktop\opusorganize\portfolio-audit\projects\batch-01.md`
(search "SubscriptionBillingDashboard").

## Suggested order (~4-5 hours total)

1. **`review/EXERCISE.md`** (60-90 min, includes trying to build first — do this before
   reading anything else in this repo, including the existing `docs/`/`docs2/`
   curriculum). Compare against `_answers/review.md` and `_answers/COMPARE.md`.
2. **`incidents/001-idempotency-caches-failures.md`** (30-45 min). Real repo, real branch
   — `git checkout training/incidents/001-idempotency-caches-failures` in a scratch
   worktree, don't touch your main checkout. Sealed answer:
   `_answers/001-idempotency-caches-failures.md`.
3. **`incidents/002-mrr-rounding-drift.md`** (30-45 min). Same process, branch
   `training/incidents/002-mrr-rounding-drift`. Sealed answer:
   `_answers/002-mrr-rounding-drift.md`.
4. **`review/characterization-tests-billingengine.md`** (60-90 min). Write the actual
   test project — there isn't one yet. No sealed answer; the tests you write are the
   deliverable.
5. **`interview/PROJECT_STORIES.md`** + **`interview/TECHNICAL_QUESTIONS.md`** (30-45
   min). Do these last, after you've actually touched the code from steps 1-4 — the
   stories reference specific findings from the review and both incidents.

## How to check out an incident branch without touching your main checkout

```powershell
git worktree add ..\netstripe-inc-001 training/incidents/001-idempotency-caches-failures
# work in ..\netstripe-inc-001
git worktree remove ..\netstripe-inc-001
```

Both incident branches are based on the same `main` commit (`a84f11b`) and both bundle
two one-line pre-existing build fixes (see `_answers/review.md` finding 0) so the project
actually compiles on that branch — those fixes are unrelated to the planted bug in each
incident; don't mistake them for part of the puzzle.

## Layout

- `review/` — blind code review exercise + the characterization-tests exercise (the
  "there are no tests" gap called out in Phase 3).
- `incidents/` — two incident briefs, no cause given. Branches:
  `training/incidents/001-idempotency-caches-failures`,
  `training/incidents/002-mrr-rounding-drift`.
- `interview/` — `PROJECT_STORIES.md` (5 STAR stories) and `TECHNICAL_QUESTIONS.md` (10
  questions). DRILL tier gets this small kit, not the full LAB-tier interview kit
  (no `SYSTEM_DESIGN.md`, `MOCK_DEFENSE.md`, or `FLASHCARDS.md` here).
- `_answers/` — sealed. Don't open before attempting the matching exercise.
  `review.md`, `COMPARE.md`, `001-idempotency-caches-failures.md`,
  `002-mrr-rounding-drift.md`.

No `ladder/`, `navigation/`, or `agentic/` directories — those are LAB-tier phases and
this project didn't get that tier (see the report at
`Desktop\opusorganize\batch1report\projects\netstripe.md` for the reasoning).
