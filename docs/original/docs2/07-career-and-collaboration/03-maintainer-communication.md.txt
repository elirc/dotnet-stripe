# 07.03 — Maintainer Communication

(Simulated OSS workflow — this repo has no real maintainers. Practice the habits; they transfer.)

The meta-skill: **ask in a way that shows you did the thinking, and respects that the maintainer's
time is the scarcest resource.** Outsourcing your thinking ("how does this work?") burns goodwill;
showing your work and asking a sharp question earns it.

## Ask without outsourcing your thinking

| Weak (outsources thinking) | Strong (shows the work) |
| --- | --- |
| "How does billing work?" | "I traced `run-cycle` → `RunBillingCycle` → `GenerateInvoice` and got `$1,111.20` for the Scale sub. Is the period-advance supposed to skip missed periods, or is that a gap (it advances by one cycle at [L84-L88](../../src/Billing.Api/Infrastructure/BillingEngine.cs#L84-L88))?" |
| "The webhook retry doesn't work." | "The backoff branch in [Deliver L41-L53](../../src/Billing.Api/Infrastructure/WebhookDeliveryWorker.cs#L41-L53) looks unreachable because the `try` body has no I/O that can throw. Intended (simulated), or a real gap?" |
| "Tests are failing." | "After adding an EUR plan, `GET /api/dashboard` 500s; I believe it's the USD seed in `CalculateMetrics` ([L262](../../src/Billing.Api/Program.cs#L262)). Want me to fix with a reporting currency or per-currency grouping?" |

The pattern: **what I tried → what I observed → the specific decision I'm asking you to make.**

## Repro report template

```markdown
**Summary:** one line.
**Environment:** .NET 8, commit/working-tree date, OS.
**Steps:**
1. ...
2. ...
**Expected:** ...
**Actual:** ... (paste real trimmed output)
**Anchor(s):** the file:line you suspect.
**Minimal example:** the smallest input that triggers it.
```

A *minimal* example is the gift. "Create a EUR plan + active subscription, GET `/api/dashboard`" is
minimal; "deploy my whole environment" is not.

## Triage: is it a bug, a gap, or a question?

- **Bug** — the code contradicts a stated contract. (Few here; the README is honest about gaps.)
- **Gap** — an unbuilt feature (dunning, auth, multi-currency). Frame as "is X in scope?" not "X is
  broken."
- **Question** — you don't yet understand it. Do one more pass of reading before asking; half of these
  dissolve.

This repo's honesty (the README and `architecture.md` list their own production gaps) means most
findings are *gaps*, not bugs. Calling a deliberate gap a "bug" reads as not having read the docs.

## Respectful disagreement

When you think a maintainer's decision is wrong:

1. **State the shared goal.** "We both want the dashboard to never 500."
2. **Show the evidence, anchored.** Not "this is bad" — "[L262](../../src/Billing.Api/Program.cs#L262)
   assumes USD; here's the input that breaks it."
3. **Offer a path, hold it loosely.** "Two options: reporting currency, or group by currency. I lean
   to grouping because it's local. What am I missing?"
4. **Disagree-and-commit.** If they decide otherwise with a reason, support it and move on.

## A note on tone

Reviews and questions that are all-criticism get ignored or resented. Lead with what's right (this
repo's `Money` design, the clock seam, the clean async are genuinely good), then raise the concern.
You're trying to be *right and easy to work with*, not just right.

## Drill

Take R-01 (currency) and write the GitHub issue you'd file: summary, minimal repro, anchor, suggested
fix, and the one decision you need from the maintainer. Keep it under 150 words. **Self-grade —
Basic:** it's clear and anchored. **Solid:** it has a minimal repro and proposes a fix. **Strong:** it
asks exactly one decision and reads like you'd be easy to collaborate with.

## Verification Notes

Illustrative communication examples; embedded anchors read-verified on 2026-06-09.
