using Billing.Api.Domain;
using Microsoft.Data.Sqlite;

namespace Billing.Api.Infrastructure;

/// <summary>A small relational envelope around immutable JSON domain records.</summary>
public sealed class BillingDatabase
{
    public string Path { get; }
    public BillingDatabase(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        // Never silently adopt a file belonging to another app, including empty files.
        var exists = File.Exists(Path);
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        var session = new BillingSession(connection, transaction);
        var marker = Convert.ToInt64(session.Scalar("PRAGMA application_id"));
        var version = Convert.ToInt64(session.Scalar("PRAGMA user_version"));
        if (exists && (marker != 1112296514 || version != 1))
            throw new InvalidDataException("Unrecognized billing database. Choose a new Billing__Database path; do not delete existing data.");
        if (!exists)
        {
            session.Execute(Schema);
            session.Execute("PRAGMA application_id=1112296514; PRAGMA user_version=1;");
        }
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path, ForeignKeys = true, DefaultTimeout = 5, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    public T Transaction<T>(Func<BillingSession, T> action, bool write = true)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: !write);
        var result = action(new BillingSession(connection, transaction));
        transaction.Commit();
        return result;
    }

    private const string Schema = """
        CREATE TABLE workspaces(id TEXT PRIMARY KEY, slug TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
          billing_time TEXT NOT NULL, secret TEXT NOT NULL);
        CREATE TABLE users(id TEXT PRIMARY KEY, email TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
          password_hash TEXT NOT NULL, salt TEXT NOT NULL);
        CREATE TABLE memberships(workspace_id TEXT NOT NULL REFERENCES workspaces(id),
          user_id TEXT NOT NULL REFERENCES users(id), role TEXT NOT NULL CHECK(role IN ('Owner','Operator','Viewer')),
          version INTEGER NOT NULL DEFAULT 1 CHECK(version>0), PRIMARY KEY(workspace_id,user_id));
        CREATE TABLE sessions(token_hash TEXT PRIMARY KEY, workspace_id TEXT NOT NULL, user_id TEXT NOT NULL,
          expires_at INTEGER NOT NULL, FOREIGN KEY(workspace_id,user_id) REFERENCES memberships(workspace_id,user_id) ON DELETE CASCADE);
        CREATE TABLE records(workspace_id TEXT NOT NULL REFERENCES workspaces(id), kind TEXT NOT NULL
          CHECK(kind IN ('customers','plans','coupons','subscriptions','usage-events','invoices','payments','payment-jobs','webhooks')),
          id TEXT NOT NULL, data TEXT NOT NULL CHECK(json_valid(data)),
          version INTEGER GENERATED ALWAYS AS (json_extract(data,'$.version')) STORED NOT NULL CHECK(version>0 AND json_type(data,'$.version')='integer'),
          CHECK(json_extract(data,'$.id') IS id), CHECK(json_extract(data,'$.workspaceId') IS workspace_id),
          PRIMARY KEY(workspace_id,kind,id));
        CREATE UNIQUE INDEX customer_email ON records(workspace_id,lower(json_extract(data,'$.email'))) WHERE kind='customers';
        CREATE UNIQUE INDEX catalog_code ON records(workspace_id,kind,lower(json_extract(data,'$.code'))) WHERE kind IN ('plans','coupons');
        CREATE UNIQUE INDEX invoice_period ON records(workspace_id,json_extract(data,'$.subscriptionId'),json_extract(data,'$.periodStart')) WHERE kind='invoices';
        CREATE UNIQUE INDEX pending_payment ON records(workspace_id,json_extract(data,'$.invoiceId')) WHERE kind='payments' AND json_extract(data,'$.status')='Pending';
        CREATE TABLE links(workspace_id TEXT NOT NULL, source_kind TEXT NOT NULL, source_id TEXT NOT NULL,
          target_kind TEXT NOT NULL, target_id TEXT NOT NULL,
          PRIMARY KEY(workspace_id,source_kind,source_id,target_kind,target_id),
          FOREIGN KEY(workspace_id,source_kind,source_id) REFERENCES records(workspace_id,kind,id) ON DELETE CASCADE,
          FOREIGN KEY(workspace_id,target_kind,target_id) REFERENCES records(workspace_id,kind,id) ON DELETE RESTRICT);
        CREATE INDEX reverse_links ON links(workspace_id,target_kind,target_id);
        CREATE TABLE receipts(workspace_id TEXT NOT NULL, user_id TEXT NOT NULL, request_key TEXT NOT NULL,
          fingerprint TEXT NOT NULL, response TEXT NOT NULL, expires_at INTEGER NOT NULL,
          PRIMARY KEY(workspace_id,user_id,request_key),
          FOREIGN KEY(workspace_id,user_id) REFERENCES memberships(workspace_id,user_id) ON DELETE CASCADE);
        CREATE TABLE audit(id INTEGER PRIMARY KEY AUTOINCREMENT, workspace_id TEXT NOT NULL REFERENCES workspaces(id),
          actor TEXT NOT NULL, action TEXT NOT NULL, record_id TEXT, at TEXT NOT NULL);
        CREATE INDEX audit_workspace ON audit(workspace_id,id DESC);
        CREATE TABLE provider_events(workspace_id TEXT NOT NULL REFERENCES workspaces(id),event_id TEXT NOT NULL,
          fingerprint TEXT NOT NULL, PRIMARY KEY(workspace_id,event_id));
        """;
}

public sealed class BillingSession(SqliteConnection connection, SqliteTransaction transaction)
{
    public SqliteCommand Command(string sql, params (string Name, object? Value)[] args)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
    public int Execute(string sql, params (string Name, object? Value)[] args)
    { using var command = Command(sql, args); return command.ExecuteNonQuery(); }
    public object? Scalar(string sql, params (string Name, object? Value)[] args)
    { using var command = Command(sql, args); return command.ExecuteScalar(); }

    public static string Kind<T>() => typeof(T).Name switch
    {
        nameof(Customer) => "customers", nameof(Plan) => "plans", nameof(Coupon) => "coupons",
        nameof(Subscription) => "subscriptions", nameof(UsageEvent) => "usage-events", nameof(Invoice) => "invoices",
        nameof(Payment) => "payments", nameof(PaymentJob) => "payment-jobs", nameof(WebhookEvent) => "webhooks",
        _ => throw new InvalidOperationException("Unregistered entity type.")
    };

    public T Get<T>(Guid workspace, Guid id) where T : IBillingEntity
    {
        var json = Scalar("SELECT data FROM records WHERE workspace_id=$w AND kind=$k AND id=$id",
            ("$w", workspace.ToString()), ("$k", Kind<T>()), ("$id", id.ToString())) as string;
        return json is null ? throw BillingError.Missing() : BillingJson.Read<T>(json);
    }

    public IReadOnlyList<T> All<T>(Guid workspace) where T : IBillingEntity
    {
        using var command = Command("SELECT data FROM records WHERE workspace_id=$w AND kind=$k ORDER BY id LIMIT 5001",
            ("$w", workspace.ToString()), ("$k", Kind<T>()));
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read()) result.Add(BillingJson.Read<T>(reader.GetString(0)));
        if (result.Count > 5000) throw BillingError.Conflict("Workshop record limit exceeded.");
        return result;
    }

    public void Insert<T>(T value) where T : IBillingEntity
    {
        if (Convert.ToInt64(Scalar("SELECT count(*) FROM records WHERE workspace_id=$w", ("$w", value.WorkspaceId.ToString()))) >= 5000)
            throw BillingError.Conflict("This local workshop is limited to 5,000 records per workspace.");
        Execute("INSERT INTO records(workspace_id,kind,id,data) VALUES($w,$k,$id,$data)",
            ("$w", value.WorkspaceId.ToString()), ("$k", Kind<T>()), ("$id", value.Id.ToString()), ("$data", BillingJson.Write(value)));
    }

    public void Update<T>(T value, long expected) where T : IBillingEntity
    {
        if (value.Version != expected + 1) throw new InvalidOperationException("Version must advance exactly once.");
        if (Execute("UPDATE records SET data=$data WHERE workspace_id=$w AND kind=$k AND id=$id AND version=$version",
            ("$data", BillingJson.Write(value)), ("$w", value.WorkspaceId.ToString()), ("$k", Kind<T>()),
            ("$id", value.Id.ToString()), ("$version", expected)) != 1)
            throw BillingError.Conflict("This record changed. Reload and review your edit.");
    }

    public void Delete<T>(Guid workspace, Guid id, long expected) where T : IBillingEntity
    {
        Get<T>(workspace, id);
        if (Execute("DELETE FROM records WHERE workspace_id=$w AND kind=$k AND id=$id AND version=$v",
            ("$w", workspace.ToString()), ("$k", Kind<T>()), ("$id", id.ToString()), ("$v", expected)) != 1)
            throw BillingError.Conflict("This record changed. Reload before deleting.");
    }

    public void Link<TSource, TTarget>(Guid workspace, Guid source, Guid target)
        where TSource : IBillingEntity where TTarget : IBillingEntity =>
        Execute("INSERT OR IGNORE INTO links VALUES($w,$sk,$s,$tk,$t)", ("$w", workspace.ToString()),
            ("$sk", Kind<TSource>()), ("$s", source.ToString()), ("$tk", Kind<TTarget>()), ("$t", target.ToString()));

    public bool Referenced<T>(Guid workspace, Guid id) where T : IBillingEntity =>
        Convert.ToInt64(Scalar("SELECT count(*) FROM links WHERE workspace_id=$w AND target_kind=$k AND target_id=$id",
            ("$w", workspace.ToString()), ("$k", Kind<T>()), ("$id", id.ToString()))) > 0;

    public void Audit(Guid workspace, string actor, string action, Guid? id = null) =>
        Execute("INSERT INTO audit(workspace_id,actor,action,record_id,at) VALUES($w,$a,$action,$id,$at)",
            ("$w", workspace.ToString()), ("$a", actor), ("$action", action), ("$id", id?.ToString()), ("$at", DateTimeOffset.UtcNow.ToString("O")));
}
