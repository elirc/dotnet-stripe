# Incident 001 — customers can't pay after a first failed attempt

**Branch**: `training/incidents/001-idempotency-caches-failures` (commit `8b0c838`, off
`main` `a84f11b`)

## What support is seeing

> Ticket #4471 (and 6 more like it this week): "Card was declined, I updated my payment
> method in [redacted external processor], but Pay Now on invoice `INV-202608-9F31A2C0`
> still says the same thing it said this morning. I've tried four times over six hours."

Ops pulled the API logs for one of these invoices:

```
09:11:47 POST /api/invoices/9f31a2c0-.../pay   Idempotency-Key: pay-inv-9f31a2c0   -> 400 {"error":"Invoice is already paid."}
   (invoice 9f31a2c0-... had been marked Paid moments earlier by the payment
    simulator worker, before the customer's dashboard had refreshed)
09:12:30 POST /api/webhooks/payment-provider   X-Webhook-Signature: (valid)        -> 200
   payload: {"eventId":"evt_772","invoiceId":"9f31a2c0-...","succeeded":false,"failureReason":"card_declined"}
   -> dashboard now shows invoice 9f31a2c0-... as Status=Open, PaidAt=null
09:14:02 POST /api/invoices/9f31a2c0-.../pay   Idempotency-Key: pay-inv-9f31a2c0   -> 400 {"error":"Invoice is already paid."}
   Idempotency-Replayed: true
14:52:19 POST /api/invoices/9f31a2c0-.../pay   Idempotency-Key: pay-inv-9f31a2c0   -> 400 {"error":"Invoice is already paid."}
   Idempotency-Replayed: true
```

The dashboard has said `Status: Open` since 09:12:30 — the invoice is genuinely
payable — but every `Pay Now` click since 09:11:47 gets the exact same 400 response,
word for word, `Idempotency-Replayed: true` header included, no matter how much time
passes or how many times the customer retries.

## What we know so far

- This only reproduces for invoices where a `Pay Now` click was retried using the same
  `Idempotency-Key` the frontend generated for that invoice (the frontend derives the key
  from the invoice id, so retries on the same invoice always reuse the same key —
  confirmed intentional, not a frontend bug).
- It is not specific to `card_declined` — one ticket shows the same pattern after a
  `409` from a duplicate coupon code, permanently stuck.
- Nothing in `BillingEngine`, `BillingStore`, or the payment worker looks obviously wrong
  in isolation.

## Your task

1. Find the root cause. You have the whole repo, not just the log excerpt above.
2. Write the regression test that would have caught this before it shipped.
3. Write the fix.
4. Write a one-paragraph postmortem: what happened, why the existing test suite (there
   isn't one — note that) didn't catch it, and what check should exist so this class of
   bug can't ship silently again.

No hints below this line. Sealed answer: `training/_answers/001-idempotency-caches-failures.md`.
