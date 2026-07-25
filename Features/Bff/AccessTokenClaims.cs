using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Plume.Features.Bff;

/// <summary>
/// Reads claims from a stored access JWT without signature verification.
/// Plume already trusts the token because it was established via Kithara authenticate/claim/refresh.
/// </summary>
public static class AccessTokenClaims
{
    public static IReadOnlyList<Claim> ReadUnvalidated(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return [];
        }

        var parts = accessToken.Split('.');
        if (parts.Length < 2)
        {
            return [];
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var claims = new List<Claim>();
            foreach (var property in root.EnumerateObject())
            {
                AppendClaims(claims, property.Name, property.Value);
            }

            return claims;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            return [];
        }
    }

    private static void AppendClaims(List<Claim> claims, string name, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                claims.Add(MapClaim(name, value.GetString() ?? string.Empty));
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                claims.Add(MapClaim(name, value.ToString()));
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        claims.Add(MapClaim(name, item.GetString() ?? string.Empty));
                    }
                }

                break;
        }
    }

    private static Claim MapClaim(string name, string value)
    {
        if (string.Equals(name, "role", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ClaimTypes.Role, StringComparison.Ordinal))
        {
            return new Claim(ClaimTypes.Role, value);
        }

        if (string.Equals(name, "sub", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ClaimTypes.NameIdentifier, StringComparison.Ordinal))
        {
            return new Claim(ClaimTypes.NameIdentifier, value);
        }

        if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ClaimTypes.Name, StringComparison.Ordinal))
        {
            return new Claim(ClaimTypes.Name, value);
        }

        return new Claim(name, value);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}
