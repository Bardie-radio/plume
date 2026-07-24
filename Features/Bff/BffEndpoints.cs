using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

public static class BffEndpoints
{
    public const string HttpClientName = "KitharaApi";

    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Host",
        "Cookie",
        "Authorization",
        "Content-Length",
    };

    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bff");
        group.MapMethods("{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"], ProxyAsync);
        return endpoints;
    }

    private static async Task ProxyAsync(
        HttpContext http,
        string? path,
        IPlumeSessionService sessions,
        IHttpClientFactory httpClientFactory,
        IOptions<KitharaOptions> kitharaOptions,
        CancellationToken cancellationToken)
    {
        var tokens = await sessions.TryGetAsync(http, cancellationToken).ConfigureAwait(false);
        if (tokens is null)
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var baseUrl = kitharaOptions.Value.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            http.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        var apiPath = string.IsNullOrEmpty(path) ? string.Empty : path;
        var targetUri = $"{baseUrl}/api/{apiPath}{http.Request.QueryString.Value}";

        // Buffer once so we can retry after refresh without re-reading a consumed body.
        byte[]? body = null;
        if (http.Request.ContentLength is > 0 || HasBody(http.Request.Method))
        {
            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            body = ms.ToArray();
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var first = await SendUpstreamAsync(
            client,
            http,
            targetUri,
            tokens.AccessToken,
            body,
            cancellationToken).ConfigureAwait(false);

        if (first.StatusCode != HttpStatusCode.Unauthorized)
        {
            await CopyResponseAsync(first, http.Response, cancellationToken).ConfigureAwait(false);
            return;
        }

        var refreshed = await TryRefreshAsync(
            client,
            baseUrl,
            tokens,
            cancellationToken).ConfigureAwait(false);

        if (refreshed is null || !sessions.TryUpdateTokens(http, refreshed))
        {
            await sessions.ClearAsync(http, cancellationToken).ConfigureAwait(false);
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var retry = await SendUpstreamAsync(
            client,
            http,
            targetUri,
            refreshed.AccessToken,
            body,
            cancellationToken).ConfigureAwait(false);

        await CopyResponseAsync(retry, http.Response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SessionTokens?> TryRefreshAsync(
        HttpClient client,
        string baseUrl,
        SessionTokens current,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/auth/refresh")
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

        var payload = await response.Content
            .ReadFromJsonAsync<RefreshResponseBody>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return null;
        }

        // Some providers omit refresh_token when it is not rotated; keep the prior value.
        var refreshToken = string.IsNullOrWhiteSpace(payload.RefreshToken)
            ? current.RefreshToken
            : payload.RefreshToken;

        // Provider stays the same across refresh; never echo tokens to the browser.
        return new SessionTokens(payload.AccessToken, refreshToken, current.ProviderId);
    }

    private static async Task<HttpResponseMessage> SendUpstreamAsync(
        HttpClient client,
        HttpContext http,
        string targetUri,
        string accessToken,
        byte[]? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(http.Request.Method), targetUri);

        if (body is { Length: > 0 })
        {
            request.Content = new ByteArrayContent(body);
            if (http.Request.ContentType is { } contentType)
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        foreach (var header in http.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CopyResponseAsync(
        HttpResponseMessage upstream,
        HttpResponse downstream,
        CancellationToken cancellationToken)
    {
        downstream.StatusCode = (int)upstream.StatusCode;

        foreach (var header in upstream.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key)
                || header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            downstream.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in upstream.Content.Headers)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                downstream.ContentType = header.Value.FirstOrDefault();
                continue;
            }

            downstream.Headers[header.Key] = header.Value.ToArray();
        }

        await upstream.Content.CopyToAsync(downstream.Body, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasBody(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method);

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
