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
- Timestamp tolerance: five minutes.

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

