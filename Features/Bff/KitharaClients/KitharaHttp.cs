using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff.KitharaClients;

/// <summary>Shared Kithara HTTP helpers for named client, base URL, refresh, and JSON reads.</summary>
public static class KitharaHttp
{
    public const string HttpClientName = "KitharaApi";

    public static string? ResolveBaseUrl(IOptions<KitharaOptions> options) =>
        ResolveBaseUrl(options.Value);

    public static string? ResolveBaseUrl(KitharaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var baseUrl = options.BaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
    }

    /// <summary>
    /// Browser URL for ICY listen. Uses <see cref="KitharaOptions.PublicBaseUrl"/> when set;
    /// otherwise a same-origin relative path (edge path map).
    /// </summary>
    public static string BuildStreamUrl(KitharaOptions options, string slug, string? listenToken = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var path = "/stream/" + Uri.EscapeDataString(slug.Trim());
        var publicBase = options.PublicBaseUrl?.TrimEnd('/');
        var url = string.IsNullOrWhiteSpace(publicBase) ? path : publicBase + path;

        if (!string.IsNullOrWhiteSpace(listenToken))
        {
            url += "?token=" + Uri.EscapeDataString(listenToken.Trim());
        }

        return url;
    }

    /// <summary>
    /// Posts refresh; when the response omits <c>refresh_token</c>, keeps the prior value.
    /// Provider id is never rotated.
    /// </summary>
    public static async Task<SessionTokens?> TryRefreshAsync(
        HttpClient client,
        string baseUrl,
        SessionTokens current,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentNullException.ThrowIfNull(current);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshRequestBody
            {
                ProviderId = current.ProviderId,
                RefreshToken = current.RefreshToken,
            }),
        };

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await TryReadJsonAsync<RefreshResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return null;
        }

        var refreshToken = string.IsNullOrWhiteSpace(payload.RefreshToken)
            ? current.RefreshToken
            : payload.RefreshToken;

        return new SessionTokens(payload.AccessToken, refreshToken, current.ProviderId);
    }

    public static async Task<T?> TryReadJsonAsync<T>(
        HttpContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            return await content
                .ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private sealed class RefreshRequestBody
    {
        [JsonPropertyName("provider_id")]
        public string ProviderId { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class RefreshResponseBody
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}
