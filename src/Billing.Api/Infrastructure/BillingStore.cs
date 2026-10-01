using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

public sealed record StoredResponse(int Status, string Json, string? ETag = null, string? Location = null);
public sealed record Mutation(string Token, string Key, string Fingerprint, long? Version);

/// <summary>Authorization, receipt, mutation and audit share one transaction.</summary>
public sealed class BillingStore(BillingDatabase database, WorkspaceAuth auth)
{
    public T Read<T>(string token, Func<BillingSession, Actor, T> read) =>
        database.Transaction(db => read(db, auth.Require(db, token)), write: false);

    public StoredResponse Write(Mutation request, string action, Func<BillingSession, Actor, object> change, int status = 200, bool owner = false)
    {
        return database.Transaction(db =>
        {
            var actor = auth.Require(db, request.Token, write: true, owner: owner);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            db.Execute("DELETE FROM receipts WHERE expires_at<=$now", ("$now", now));
            using (var command = db.Command("SELECT fingerprint,response FROM receipts WHERE workspace_id=$w AND user_id=$u AND request_key=$key",
                ("$w", actor.WorkspaceId.ToString()), ("$u", actor.UserId.ToString()), ("$key", request.Key)))
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (reader.GetString(0) != request.Fingerprint) throw BillingError.Conflict("This idempotency key belongs to a different request.");
                    return BillingJson.Read<StoredResponse>(reader.GetString(1));
                }
            }
            var result = change(db, actor);
            var entity = result as IBillingEntity;
            var response = new StoredResponse(status, System.Text.Json.JsonSerializer.Serialize(result, result.GetType(), BillingJson.Options),
                entity is null ? null : $"\"v{entity.Version}\"");
            db.Audit(actor.WorkspaceId, actor.UserId.ToString(), action, entity?.Id);
            db.Execute("INSERT INTO receipts VALUES($w,$u,$key,$fp,$response,$expiry)", ("$w", actor.WorkspaceId.ToString()),
                ("$u", actor.UserId.ToString()), ("$key", request.Key), ("$fp", request.Fingerprint),
                ("$response", BillingJson.Write(response)), ("$expiry", now + 86_400));
            return response;
        });
    }
}
