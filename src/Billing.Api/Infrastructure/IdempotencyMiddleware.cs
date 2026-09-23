using Billing.Api.Domain;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Billing.Api.Infrastructure;

public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IdempotencyStore store, IOptions<BillingOptions> options)
    {
        if (!RequiresIdempotency(context.Request))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues))
        {
            await next(context);
            return;
        }

        var idempotencyKey = idempotencyKeyValues.ToString().Trim();
        if (idempotencyKey.Length is < 8 or > 200)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Idempotency-Key must be between 8 and 200 characters." });
            return;
        }

        var body = await ReadRequestBody(context.Request);
        var requestHash = Hash($"{context.Request.Method}:{context.Request.Path}:{body}");
        var cacheKey = $"{context.Request.Method}:{context.Request.Path}:{idempotencyKey}";

        if (store.TryGet(cacheKey, out var cached))
        {
            if (!string.Equals(cached.RequestHash, requestHash, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { error = "Idempotency key reused with a different request payload." });
                return;
            }

            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = cached.ContentType;
            context.Response.Headers["Idempotency-Replayed"] = "true";
            await context.Response.WriteAsync(cached.ResponseBody);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        buffer.Position = 0;
        var responseBody = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
        context.Response.Body = originalBody;

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.IdempotencyTtlHours));
            store.Set(cacheKey, requestHash, context.Response.StatusCode, context.Response.ContentType ?? "application/json", responseBody, ttl);
        }
    }

    private static bool RequiresIdempotency(HttpRequest request) =>
        request.Path.StartsWithSegments("/api") &&
        request.Method is "POST" or "PUT" or "PATCH" or "DELETE";

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}

public sealed class IdempotencyStore(IBillingClock clock)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IdempotencyEntry> _entries = [];

    public bool TryGet(string key, out IdempotencyEntry entry)
    {
        lock (_gate)
        {
            PurgeExpired();
            return _entries.TryGetValue(key, out entry!);
        }
    }

    public void Set(string key, string requestHash, int statusCode, string contentType, string responseBody, TimeSpan ttl)
    {
        lock (_gate)
        {
            _entries[key] = new IdempotencyEntry(requestHash, statusCode, contentType, responseBody, clock.UtcNow.Add(ttl));
        }
    }

    private void PurgeExpired()
    {
        var now = clock.UtcNow;
        foreach (var key in _entries.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray())
        {
            _entries.Remove(key);
        }
    }
}

public sealed record IdempotencyEntry(
    string RequestHash,
    int StatusCode,
    string ContentType,
    string ResponseBody,
    DateTimeOffset ExpiresAt);

