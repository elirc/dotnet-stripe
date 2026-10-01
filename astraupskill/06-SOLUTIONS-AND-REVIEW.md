# Solution guidance and review standards

Use this guide after making an attempt. It describes the reasoning a reviewer should expect, rather than handing you a large patch to paste. The exercises in [Practice](05-PRACTICE.md) are extensions; the current implementation remains the reference for existing behavior.

## Money and discount exercise

For a base fee of USD 20, 100 included units and 150 actual units at USD 0.10, the subtotal is USD 25. A 20% coupon produces a USD 5 discount and USD 20 total. A fixed USD 50 coupon is capped at the USD 25 subtotal, leaving zero. A zero-total invoice is marked Paid without a payment attempt. An expired coupon contributes zero discount when it was already expired at the period start.

The important review question is not whether the arithmetic looks familiar. Ask whether the test asserts integer minor units, a matching currency, the chosen rounding rule and the validity timestamp. A USD coupon cannot be applied as though it were EUR. A delayed run should not change an agreement's historical discount simply because the operator clicked later.

## Notes-field solution outline

An optional property with a safe default lets existing JSON records remain readable. For example, a model property initialized to an empty string can represent no note without making every consumer handle null. A request default and validation rule must agree with that choice. Normalize line endings only if the product decision calls for it; do not trim away meaningful content without explaining why.

The version should still advance through the existing `Update` helper, and the response should still be stored by [BillingStore](../src/Billing.Api/Infrastructure/BillingStore.cs). Creating a special notes endpoint that bypasses that transaction would duplicate infrastructure and weaken the retry contract. A reviewer should look for a test that reads an older record lacking the property, not merely a test of a newly created note.

## Archive-filter solution outline

Parse the filter into a small known set. Add the same SQL predicate to both count and page queries. Continue passing values as parameters. If you build a dynamic SQL fragment, select it only from constant fragments controlled by the program; never interpolate arbitrary query text.

The React screen should reset offset to zero when the filter changes and prevent an old response from replacing the new results. The existing generation counter demonstrates this pattern for searches. A useful test creates more matching records than fit on one page and checks that page two contains the expected remainder. A test with only three records cannot reveal a pagination bug.

## Relationship-projection solution outline

A compact lookup response might contain `id`, `label`, `status` and `version`. The server can derive a label such as `Ada Studio · Studio Hybrid · Active` while keeping the underlying UUID as identity. The selector must not use that display string as the database key.

Joining related records should include workspace in every relationship. A missing or inactive record discovered at submit time should produce a recoverable error while preserving the form. Do not assume that a valid dropdown selection stays valid indefinitely; selection and submission happen at different moments.

## Adjustment-workflow review

A good design creates a new immutable document linked to its source invoice. It explains how the remaining balance is calculated and whether multiple adjustments can exceed the original amount. It names one transaction that inserts the adjustment, records its relationship, updates any derived balance and writes audit and receipt rows.

Ask the author to demonstrate two concurrent adjustment requests. A naive read-then-write without a shared transaction can let both consume the same remaining balance. Also ask what happens when an adjustment response is lost. The operation key should identify one intended adjustment, while a different key can represent a separate legitimate correction subject to balance rules.

### A worked exemplar for exercise 5

Exercises 5–7 are design exercises, and everything else in this chapter is a review standard rather than an answer. That is deliberate — but it leaves you with no example of what "a reviewable design" actually looks like. So here is one for exercise 5, written to the standard the review above demands. Use it as a model for the shape and the level of detail, not as the answer: your design should differ, and you should be able to say why.

---

**Design: credit notes for over-recorded usage**

**Problem.** An invoice charged 150 `api_calls`, of which 20 were recorded in error. The invoice is issued and possibly paid. Invoiced usage and paid invoice amounts are immutable (chapter 02), so no field on the invoice may be edited.

**Decision.** Introduce a new immutable document, `CreditNote`, that references the original invoice. The invoice never changes; the *balance* is derived. This mirrors how real accounting systems work, and it means the audit question "what did we tell this customer in March?" always has one answer.

**Model.**

```
CreditNote
  Id              Guid
  WorkspaceId     Guid          -- tenancy, as on every entity
  Number          string        -- "CN-XXXXXXXXXXXX", same generator shape as INV-
  InvoiceId       Guid          -- the source invoice; linked, not duplicated
  Reason          string        -- required, free text, shown to the customer
  Lines           InvoiceLine[] -- snapshotted, same type the invoice uses
  Amount          Money         -- must equal the sum of Lines, same currency as the invoice
  CreatedAt       DateTimeOffset
  Version         long          -- so it participates in the same ETag scheme
```

Derived, never stored: `Invoice.RemainingBalance = Invoice.Total - sum(CreditNote.Amount where InvoiceId = Invoice.Id)`. Storing it would create a second source of truth and a reconciliation bug; computing it is cheap at this scale and is the safer default until measurement says otherwise (which is exercise 6's whole subject).

**Rules, and where each is enforced.**

| Rule | Enforced where | Failure |
|---|---|---|
| Currency must equal the invoice's currency | service, before the write | `400` — and note `Money.Subtract` would throw anyway; the explicit check exists to produce a usable message |
| `Amount` > 0 | service | `400` |
| Sum of credit notes must not exceed `Invoice.Total` | **inside the transaction**, re-reading the existing notes | `409`, "This credit note exceeds the invoice's remaining balance." |
| The invoice must exist in this workspace | `db.Get<Invoice>(w, id)` | `404` — a UUID is not a permission boundary |
| The invoice must not be `Void` | service | `409` |
| Operator role required | existing workspace membership check | `403` |

**Transaction boundary.** One transaction, through `BillingStore.Mutate` so the audit row and the receipt come along:

1. Read the invoice (`db.Get<Invoice>(w, invoiceId)`) — this also enforces tenancy.
2. Read existing credit notes for that invoice and sum them.
3. Check the new amount against the remaining balance. **This read and the following insert must be in the same transaction**; see below.
4. Insert the `CreditNote` and `db.Link<CreditNote, Invoice>(w, note.Id, invoice.Id)`.
5. If the invoice becomes fully credited, enqueue an `invoice.credited` outbox event.
6. Audit row and receipt, written by `Mutate`.

**Concurrency — the interesting part.** Two Operators each submit a 15.00 credit note against a 25.00 invoice at the same time. Read-then-write outside a transaction lets both see "remaining 25.00" and both commit, producing 30.00 of credit against a 25.00 invoice. Steps 2–4 above being in one immediate transaction is what prevents it: SQLite permits a single writer, so the second transaction waits, re-reads the true sum, and fails the check with `409`. A learner who has read part 2 of chapter 02 should recognise this as the *partial intent* mechanism doing work that neither ETags nor idempotency keys could do — the two requests are not stale (both read current state) and not repeated (they are genuinely different intentions).

**Version expectations.** The write targets the `CreditNote`, which does not exist yet, so `If-Match` is not meaningful on creation — the endpoint should return `428`-free and simply not require a precondition, exactly as `POST /api/customers` does not. Requiring the *invoice's* version instead is a design option worth arguing about; my judgement is no, because the invoice genuinely has not changed, and demanding its ETag would make the API lie about what is being modified.

**Idempotency.** Standard: one key names one intended credit note. The same key with the same body replays the stored `201`. The same key with a different amount returns `409`. A *different* key with an identical body creates a second credit note, and that is correct — two people can legitimately credit 15.00 twice, subject to the balance rule that will reject the second one here.

**Tests this design owes.**

- `CreditNoteReducesRemainingBalance` — happy path, balance asserted by re-reading the invoice.
- `CreditNoteCannotExceedRemainingBalance` — asserts `409` **and** that no `CreditNote` row was written.
- `ConcurrentCreditNotesCannotOverdraw` — two tasks, one transaction each; asserts exactly one success and a total that never exceeds the invoice.
- `SameKeyReplaysTheSameCreditNote` versus `DifferentKeyCreatesASecondCreditNote` — the pair that proves the implementation distinguishes a retry from a new intention. Without both, you have not tested idempotency, you have tested that a receipt exists.
- `CreditNoteInAnotherWorkspaceReturns404` — tenancy, every time.
- `MixedCurrencyCreditNoteIsRejected` — the `Money` contract.

**Explicitly out of scope.** Tax treatment, refunds to a payment method, accounting-standard compliance and reporting. This is a learning implementation of a document workflow; it is not a general ledger, and saying so in the design is part of the design.

---

Two things to notice about the shape of that document, because they are what make it reviewable. It names the *failure* for every rule, not just the rule. And it says which decisions were judgement calls (the `If-Match` question, the derived balance) and argues them, rather than presenting the whole thing as inevitable. A reviewer can disagree with a named decision; they can only be puzzled by an unstated one.

## Storage and outbox review

For storage changes, require measured evidence and a reversible migration procedure. A fully normalized schema can improve constraints and queries, but introduces migration and mapping work. The present JSON envelope is a deliberate teaching compromise, not proof that normalization is unnecessary.

For a real outbox sender, require an explicit at-least-once delivery contract. A successful HTTP response, a timeout and a process crash after sending are different observations. A lease avoids simultaneous ownership but cannot prove that a remote receiver did not already act. Receiver-side event IDs and durable deduplication are part of the solution.

## A practical review rubric

Score each category from 0 to 2: behavior is stated precisely; permissions are enforced server-side; validation covers malformed and conflicting input; transaction boundaries match the business operation; concurrency and retry behavior are demonstrated; tests exercise observable outcomes; setup instructions work from a clean environment; limitations are stated honestly.

A score of 0 means missing, 1 means partially addressed, and 2 means explained with evidence. A change that compiles but silently overwrites another user's edit should not pass review because its visual interface looks polished. Conversely, avoid adding abstractions that no tested requirement needs. The goal is a coherent change another engineer can maintain and explain.
