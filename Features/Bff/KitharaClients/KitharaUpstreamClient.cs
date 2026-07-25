using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff.KitharaClients;

/// <summary>
/// Authenticated Kithara <c>/api/…</c> calls using the Plume session Bearer,
/// with a single refresh+retry on upstream <c>401</c>.
/// </summary>
public interface IKitharaUpstreamClient
{
    /// <summary>
    /// Returns <c>null</c> when there is no session or refresh fails (session cleared).
    /// Caller owns disposing the response.
    /// </summary>
    Task<HttpResponseMessage?> SendAsync(
        HttpContext http,
        HttpMethod method,
        string apiPath,
        HttpContent? content = null,
        CancellationToken cancellationToken = default);
}

public sealed class KitharaUpstreamClient(
    IHttpClientFactory httpClientFactory,
    IPlumeSessionService sessions,
    IOptions<KitharaOptions> kitharaOptions,
    IOptions<SessionOptions> sessionOptions) : IKitharaUpstreamClient
{
    /// <summary>One refresh at a time per session — parallel list calls must not rotate thrash.</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshGates = new(StringComparer.Ordinal);

    public async Task<HttpResponseMessage?> SendAsync(
        HttpContext http,
        HttpMethod method,
        string apiPath,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiPath);

        var tokens = await sessions.TryGetAsync(http, cancellationToken).ConfigureAwait(false);
        if (tokens is null)
        {
            return null;
        }

        var baseUrl = KitharaHttp.ResolveBaseUrl(kitharaOptions);
        if (baseUrl is null)
        {
            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        }

        var targetUri = $"{baseUrl}/api/{apiPath.TrimStart('/')}";
        var client = httpClientFactory.CreateClient(KitharaHttp.HttpClientName);

        // Buffer body so refresh retry can resend.
        byte[]? bodyBytes = null;
        MediaTypeHeaderValue? contentType = null;
        if (content is not null)
        {
            bodyBytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            contentType = content.Headers.ContentType;
            content.Dispose();
        }

        var first = await SendOnceAsync(client, method, targetUri, tokens.AccessToken, bodyBytes, contentType, cancellationToken)
            .ConfigureAwait(false);

        if (first.StatusCode != HttpStatusCode.Unauthorized)
        {
            return first;
        }

        first.Dispose();

        var refreshed = await RefreshSessionExclusiveAsync(http, client, baseUrl, tokens, cancellationToken)
            .ConfigureAwait(false);
        if (refreshed is null)
        {
            return null;
        }

        return await SendOnceAsync(
                client,
                method,
                targetUri,
                refreshed.AccessToken,
                bodyBytes,
                contentType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SessionTokens?> RefreshSessionExclusiveAsync(
        HttpContext http,
        HttpClient client,
        string baseUrl,
        SessionTokens observed,
        CancellationToken cancellationToken)
    {
        var cookieName = sessionOptions.Value.CookieName;
        if (!http.Request.Cookies.TryGetValue(cookieName, out var sessionId)
            || string.IsNullOrWhiteSpace(sessionId))
        {
            await sessions.ClearAsync(http, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var gate = RefreshGates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another parallel caller may have already refreshed past the token we saw.
            var latest = await sessions.TryGetAsync(http, cancellationToken).ConfigureAwait(false);
            if (latest is null)
            {
                return null;
            }

            if (!string.Equals(latest.AccessToken, observed.AccessToken, StringComparison.Ordinal))
            {
                return latest;
            }

            var refreshed = await KitharaHttp
                .TryRefreshAsync(client, baseUrl, latest, cancellationToken)
                .ConfigureAwait(false);
            if (refreshed is null || !sessions.TryUpdateTokens(http, refreshed))
            {
                await sessions.ClearAsync(http, cancellationToken).ConfigureAwait(false);
                return null;
            }

            return refreshed;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<HttpResponseMessage> SendOnceAsync(
        HttpClient client,
        HttpMethod method,
        string targetUri,
        string accessToken,
        byte[]? bodyBytes,
        MediaTypeHeaderValue? contentType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, targetUri);
        if (bodyBytes is { Length: > 0 })
        {
            request.Content = new ByteArrayContent(bodyBytes);
            if (contentType is not null)
            {
                request.Content.Headers.ContentType = contentType;
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
