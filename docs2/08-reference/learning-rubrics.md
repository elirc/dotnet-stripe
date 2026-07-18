# Learning Rubrics

Observable behaviors at each level, plus self-assessment checklists. "Observable" means *someone could
watch you work and tell* — not "you feel like you know it." Re-grade yourself monthly against this repo.

## The mindset ladder (the spine of everything)

| Level | The question they ask | What you'd hear |
| --- | --- | --- |
| Junior | "How do I make it work?" | "I added the endpoint and it returns 200." |
| Mid | "Is this the right pattern?" | "It works, but it duplicates the catch-block — should we extract a filter?" |
| Senior | "What does this commit us to, who pays, how do we reduce risk?" | "This couples the contract to the entity; renaming a field breaks the client. Here's the migration + rollback." |

## Junior — observable behaviors

You can:
- [ ] Run both apps and hit an endpoint ([00-fast-track](../00-fast-track.md)).
- [ ] Follow one feature end to end and name the layers it crosses ([01/05](../01-codebase-cartography/05-key-flows.md)).
- [ ] Make a small additive change following an existing pattern ([06/01](../06-contribution-practice/01-good-first-tickets.md)).
- [ ] Read a method and state its inputs/outputs/side effects ([04/01](../04-code-reading-gym/01-annotation-drills.md)).
- [ ] Explain *why* money is integer minor units, not a `double`.
- [ ] Write a basic unit test with the injected clock ([05/02 Recipe 1](../05-quality-engineering/02-writing-tests-here.md)).

**Self-check:** Can you add `GET /api/coupons/{id}` *and* a test for it, and write a PR description that
names the pattern you copied? If yes, you're past junior on this repo.

## Mid-level — observable behaviors

You can do all of the above, plus:
- [ ] Design a change that crosses layers (engine + API + UI + tests) ([06/02](../06-contribution-practice/02-mid-level-feature-tickets.md)).
- [ ] Debug methodically — reproduce, narrow to a boundary, hypothesize, test cheaply ([05/03](../05-quality-engineering/03-systematic-debugging.md)).
- [ ] Spot a pattern *and* its failure mode (idempotency race, N+1, captive dependency) ([03/05](../03-architecture-and-patterns/05-pattern-catalog.md)).
- [ ] Review a PR with labeled severities and anchored comments ([07/01](../07-career-and-collaboration/01-code-review-mindset.md)).
- [ ] Write tests for failure paths (validation, the EUR currency bug), not just the happy path.
- [ ] Explain a DI lifetime choice and when it would be a bug.

**Self-check:** Given "a customer was charged twice," can you locate R-05, explain why the
invoice-status guard doesn't prevent the duplicate *record*, and write the regression test? If yes,
you're operating at mid-level.

## Senior — observable behaviors

All of the above, plus:
- [ ] Critique the architecture: strongest choices, ranked risks, "if I owned this 3 months" ([03/06](../03-architecture-and-patterns/06-architecture-critique.md)).
- [ ] Choose between viable designs and state the switch signal ([03/07](../03-architecture-and-patterns/07-comparative-implementations.md)).
- [ ] Plan a migration with characterization tests + rollback (the EF Core drill).
- [ ] Reason about security/reliability as *invariants* (auth as a non-bypassable boundary, not per-endpoint checks).
- [ ] Sequence work by **risk reduction per unit of change**, and defend *not* doing things this sprint.
- [ ] Write an RFC that surfaces its own trade-offs ([07/02](../07-career-and-collaboration/02-writing-prs-and-rfcs.md)).

**Self-check:** Can you defend "in-memory store over a database here" *and* name the exact signals
(durability, multi-replica, reporting, transactions) that would flip your decision — and sketch the
migration with a rollback? If yes, you're reasoning like a senior on this repo.

## Per-drill grading scale (used throughout the suite)

| Grade | Means |
| --- | --- |
| **Basic** | You got the surface-level correct answer. |
| **Solid** | You got it right *and* explained the trade-off / failure mode. |
| **Strong** | You connected it to the wider design, named what it commits the team to, and said what you'd do. |

The gap between **Solid** and **Strong** is the gap between mid-level and senior: Solid *understands*;
Strong *decides and owns the consequence*.

## A self-assessment routine

1. Pick a flow or risk. Explain it out loud at all three mindset levels (junior/mid/senior).
2. Do the matching drill; grade Basic/Solid/Strong honestly.
3. If you're stuck at Basic, the gap is *knowledge* — re-read the anchored code.
4. If you're stuck at Solid, the gap is *judgment* — do a comparison card or a critique kata.
5. Track which modules you're consistently **Strong** in; those are your interview talking points
   ([07/04](../07-career-and-collaboration/04-interview-prep-from-this-repo.md)).

## Verification Notes

Rubric behaviors map to the suite's modules and to read-verified risks (2026-06-09). The grading scale
is the same Basic/Solid/Strong used in every drill, so self-grading is consistent across the suite.
