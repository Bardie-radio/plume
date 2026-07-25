using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Plume.Tests.Fixtures;

/// <summary>Unsigned JWT payloads for Plume session claim-mapping tests (signature never verified).</summary>
public static class TestAccessJwt
{
    public static string Create(
        string subject = "user-1",
        string providerId = "bes",
        IReadOnlyList<string>? roles = null,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sub"] = subject,
            ["bardie_provider"] = providerId,
            ["token_use"] = "access",
        };

        if (roles is { Count: 1 })
        {
            payload[ClaimTypes.Role] = roles[0];
        }
        else if (roles is { Count: > 1 })
        {
            payload[ClaimTypes.Role] = roles.ToArray();
        }

        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                payload[key] = value;
            }
        }

        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var body = Base64Url(JsonSerializer.Serialize(payload));
        return $"{header}.{body}.sig";
    }

    public static string CreateAdmin(string providerId = "bes", string subject = "user-1") =>
        Create(subject: subject, providerId: providerId, roles: ["admin"]);

    public static string CreateUser(string providerId = "bes", string subject = "user-1") =>
        Create(subject: subject, providerId: providerId, roles: ["user"]);

    public static string CreateClaim() =>
        Create(
            subject: "claim-user",
            providerId: "kithara.claim",
            roles: null,
            extra: new Dictionary<string, string> { ["bardie_bind_only"] = "true" });

    private static string Base64Url(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
