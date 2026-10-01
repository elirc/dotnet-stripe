using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

public sealed record Actor(Guid WorkspaceId, Guid UserId, string Role, string Name, string WorkspaceName, string Slug);
public sealed record RegisterRequest(string Email, string Password, string Name, string WorkspaceName, string Slug);
public sealed record LoginRequest(string Email, string Password, string Slug);
public sealed record MemberRequest(string Email, string Password, string Name, string Role);
public sealed record RoleRequest(string Role);
public sealed record SessionResponse(string Token, Actor Actor, DateTimeOffset ExpiresAt);

public static partial class Input
{
    public static string Text(string? value, string field, int max = 120)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > max || text.Any(char.IsControl))
            throw BillingError.Invalid($"{field} must contain 1–{max} characters without control characters.");
        return text;
    }
    public static string Email(string? value)
    {
        var email = Text(value, "Email", 254).ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !email.Contains('.'))
            throw BillingError.Invalid("Enter a valid email address.");
        return email;
    }
    public static string Code(string? value)
    {
        var code = Text(value, "Code", 48).ToLowerInvariant();
        if (!CodePattern().IsMatch(code)) throw BillingError.Invalid("Codes use lowercase letters, digits and hyphens; start with a letter.");
        return code;
    }
    [GeneratedRegex("^[a-z][a-z0-9-]{0,47}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
    public static string Password(string? password)
    {
        if (password is null || password.Length < 12 || password.Length > 128)
            throw BillingError.Invalid("Use a password containing 12–128 characters.");
        return password;
    }
    public static string Role(string? role) => role is "Operator" or "Viewer" ? role :
        throw BillingError.Invalid("New members can be Operator or Viewer. The initial Owner is protected.");
    public static T EnumValue<T>(T value) where T : struct, Enum => Enum.IsDefined(value) ? value :
        throw BillingError.Invalid("Unknown enum value.");
}

public sealed class WorkspaceAuth
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string PasswordHash(string password, byte[] salt) => Convert.ToHexString(
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32));

    public SessionResponse Register(BillingSession db, RegisterRequest request)
    {
        var email = Input.Email(request.Email);
        var password = Input.Password(request.Password);
        var name = Input.Text(request.Name, "Name");
        var workspaceName = Input.Text(request.WorkspaceName, "Workspace name");
        var slug = Input.Code(request.Slug);
        var user = Guid.NewGuid(); var workspace = Guid.NewGuid();
        InsertUser(db, user, email, password, name);
        db.Execute("INSERT INTO workspaces VALUES($id,$slug,$name,$time,$secret)", ("$id", workspace.ToString()),
            ("$slug", slug), ("$name", workspaceName), ("$time", DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString("O")),
            ("$secret", Convert.ToHexString(RandomNumberGenerator.GetBytes(32))));
        db.Execute("INSERT INTO memberships(workspace_id,user_id,role) VALUES($w,$u,'Owner')",
            ("$w", workspace.ToString()), ("$u", user.ToString()));
        db.Audit(workspace, user.ToString(), "workspace.registered");
        return Session(db, new Actor(workspace, user, "Owner", name, workspaceName, slug));
    }

    private static void InsertUser(BillingSession db, Guid user, string email, string password, string name)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        db.Execute("INSERT INTO users VALUES($id,$email,$name,$hash,$salt)", ("$id", user.ToString()),
            ("$email", email), ("$name", name), ("$hash", PasswordHash(password, salt)), ("$salt", Convert.ToHexString(salt)));
    }

    public SessionResponse Login(BillingSession db, LoginRequest request)
    {
        var email = Input.Email(request.Email); var password = Input.Password(request.Password);
        var slug = Input.Code(request.Slug);
        using var command = db.Command("""
            SELECT u.id,u.name,u.password_hash,u.salt,w.id,w.name,m.role FROM users u
            JOIN memberships m ON m.user_id=u.id JOIN workspaces w ON w.id=m.workspace_id
            WHERE u.email=$email AND w.slug=$slug
            """, ("$email", email), ("$slug", slug));
        Actor? actor = null;
        var expected = new byte[32]; var salt = new byte[16];
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                expected = Convert.FromHexString(reader.GetString(2)); salt = Convert.FromHexString(reader.GetString(3));
                actor = new Actor(Guid.Parse(reader.GetString(4)), Guid.Parse(reader.GetString(0)), reader.GetString(6),
                    reader.GetString(1), reader.GetString(5), slug);
            }
        }
        var actual = Convert.FromHexString(PasswordHash(password, salt));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual) || actor is null)
            throw new BillingError(401, "credentials", "Email, password or workspace code is incorrect.");
        return Session(db, actor);
    }

    private static SessionResponse Session(BillingSession db, Actor actor)
    {
        var now = DateTimeOffset.UtcNow;
        db.Execute("DELETE FROM sessions WHERE expires_at<=$now", ("$now", now.ToUnixTimeSeconds()));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = now.AddHours(8);
        db.Execute("INSERT INTO sessions VALUES($hash,$w,$u,$expires)", ("$hash", Hash(token)),
            ("$w", actor.WorkspaceId.ToString()), ("$u", actor.UserId.ToString()), ("$expires", expires.ToUnixTimeSeconds()));
        return new SessionResponse(token, actor, expires);
    }

    public Actor Require(BillingSession db, string? token, bool write = false, bool owner = false)
    {
        if (token is null || token.Length != 64) throw new BillingError(401, "session", "Sign in to a workspace.");
        using var command = db.Command("""
            SELECT w.id,u.id,m.role,u.name,w.name,w.slug FROM sessions s
            JOIN memberships m ON m.workspace_id=s.workspace_id AND m.user_id=s.user_id
            JOIN users u ON u.id=m.user_id JOIN workspaces w ON w.id=m.workspace_id
            WHERE s.token_hash=$hash AND s.expires_at>$now
            """, ("$hash", Hash(token)), ("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new BillingError(401, "session", "Your session expired or was revoked. Sign in again.");
        var actor = new Actor(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5));
        if ((owner && actor.Role != "Owner") || (write && actor.Role == "Viewer"))
            throw new BillingError(403, "permission", "Your current workspace role cannot perform this action.");
        return actor;
    }

    public object Members(BillingSession db, Actor actor)
    {
        if (actor.Role != "Owner") throw new BillingError(403, "permission", "Owner role required.");
        using var command = db.Command("SELECT u.id,u.name,u.email,m.role,m.version FROM memberships m JOIN users u ON u.id=m.user_id WHERE workspace_id=$w ORDER BY u.email LIMIT 101",
            ("$w", actor.WorkspaceId.ToString()));
        using var reader = command.ExecuteReader(); var rows = new List<object>();
        while (reader.Read()) rows.Add(new { id = reader.GetString(0), name = reader.GetString(1), email = reader.GetString(2), role = reader.GetString(3), version = reader.GetInt64(4) });
        return rows;
    }

    public object AddMember(BillingSession db, Actor actor, MemberRequest request)
    {
        if (actor.Role != "Owner") throw new BillingError(403, "permission", "Owner role required.");
        if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM memberships WHERE workspace_id=$w", ("$w", actor.WorkspaceId.ToString()))) >= 100)
            throw BillingError.Conflict("Workshop membership limit is 100.");
        var id = Guid.NewGuid(); var email = Input.Email(request.Email); var name = Input.Text(request.Name, "Name");
        var role = Input.Role(request.Role);
        InsertUser(db, id, email, Input.Password(request.Password), name);
        db.Execute("INSERT INTO memberships(workspace_id,user_id,role) VALUES($w,$u,$r)", ("$w", actor.WorkspaceId.ToString()), ("$u", id.ToString()), ("$r", role));
        return new { id, name, email, role, version = 1 };
    }

    public object ChangeMember(BillingSession db, Actor actor, Guid id, long version, string? role)
    {
        if (actor.Role != "Owner") throw new BillingError(403, "permission", "Owner role required.");
        var current = db.Scalar("SELECT role FROM memberships WHERE workspace_id=$w AND user_id=$u", ("$w", actor.WorkspaceId.ToString()), ("$u", id.ToString())) as string;
        if (current is null) throw BillingError.Missing();
        if (current == "Owner") throw BillingError.Conflict("The founding Owner membership is protected.");
        var changed = role is null
            ? db.Execute("DELETE FROM memberships WHERE workspace_id=$w AND user_id=$u AND version=$v", ("$w", actor.WorkspaceId.ToString()), ("$u", id.ToString()), ("$v", version))
            : db.Execute("UPDATE memberships SET role=$r,version=version+1 WHERE workspace_id=$w AND user_id=$u AND version=$v", ("$r", Input.Role(role)), ("$w", actor.WorkspaceId.ToString()), ("$u", id.ToString()), ("$v", version));
        if (changed != 1) throw BillingError.Conflict("Membership changed; reload before editing.");
        return new { id, role, version = version + 1, deleted = role is null };
    }
}
