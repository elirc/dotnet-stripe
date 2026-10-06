# Webhook Design

Inbound provider webhooks are accepted at:

```text
POST /api/webhooks/payment-provider
```

The endpoint verifies:

- `X-Webhook-Timestamp`: Unix seconds.
- `X-Webhook-Signature`: HMAC SHA-256 hex digest.
- Signed payload: `{timestamp}.{rawBody}`.
- Secret: `Billing:WebhookSigningSecret`.
- Timestamp tolerance: five minutes **into the past only**. The check is `DateTimeOffset.UtcNow - timestamp > 5 min`
  (`src/Billing.Api/Infrastructure/WebhookSignatureVerifier.cs:25`), so a timestamp in the *future* is never
  rejected. A captured, validly signed request with a far-future timestamp can be replayed indefinitely. A
  symmetric check (`Math.Abs` of the difference) closes that.

What the endpoint does **not** do (verified 2026-10-06 against `src/Billing.Api/Program.cs:223-250`):

- It doesn't deduplicate on `eventId`. The id is echoed in the response (line 244) and never stored, so a
  provider retry applies the payment result again (`training/_answers/review.md` finding 2).
- It doesn't require the `Idempotency-Key` header used in the example below. `IdempotencyMiddleware` lets
  requests without that header straight through (`IdempotencyMiddleware.cs:17-21`), and real providers don't
  send it.

Example payload:

```json
{
  "eventId": "evt_123",
  "eventType": "payment.succeeded",
  "invoiceId": "00000000-0000-0000-0000-000000000000",
  "succeeded": true,
  "providerReference": "pay_123",
  "failureReason": null
}
```

PowerShell signature example:

```powershell
$body = '{"eventId":"evt_123","eventType":"payment.succeeded","invoiceId":"00000000-0000-0000-0000-000000000000","succeeded":true,"providerReference":"pay_123","failureReason":null}'
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$secret = 'dev_secret_replace_me'
$hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$bytes = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$timestamp.$body"))
$signature = [Convert]::ToHexString($bytes).ToLowerInvariant()
Invoke-RestMethod http://localhost:5088/api/webhooks/payment-provider `
  -Method Post `
  -Headers @{ 'X-Webhook-Timestamp' = $timestamp; 'X-Webhook-Signature' = $signature; 'Idempotency-Key' = 'evt_12345' } `
  -ContentType 'application/json' `
  -Body $body
```

