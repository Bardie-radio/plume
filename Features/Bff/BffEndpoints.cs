using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Features.Bff;

public static class BffEndpoints
{
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

        group.MapBffAuthEndpoints();

        // Sole proxied auth path — remaining /auth/* is owned by MapBffAuthEndpoints.
        group.MapMethods("/auth/me", ["GET", "HEAD"], ProxyAsync);

        // Non-auth API mirror. Constraint prevents /auth/* from selecting this endpoint.
        group.MapMethods(
            "{**path:bffNonAuth}",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
            ProxyAsync);

        return endpoints;
    }

    private static async Task ProxyAsync(
        HttpContext http,
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

        var baseUrl = KitharaHttp.ResolveBaseUrl(kitharaOptions);
        if (baseUrl is null)
        {
            http.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        var apiPath = GetApiPath(http.Request.Path);
        var targetUri = $"{baseUrl}/api/{apiPath}{http.Request.QueryString.Value}";

        // Buffer once so we can retry after refresh without re-reading a consumed body.
        byte[]? body = null;
        if (http.Request.ContentLength is > 0 || HasBody(http.Request.Method))
        {
            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            body = ms.ToArray();
        }

        var client = httpClientFactory.CreateClient(KitharaHttp.HttpClientName);
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

        var refreshed = await KitharaHttp
            .TryRefreshAsync(client, baseUrl, tokens, cancellationToken)
            .ConfigureAwait(false);

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

    /// <summary>Strip the <c>/bff</c> prefix so upstream is <c>/api/…</c>.</summary>
    private static string GetApiPath(PathString requestPath)
    {
        var value = requestPath.Value ?? string.Empty;
        if (value.StartsWith("/bff/", StringComparison.OrdinalIgnoreCase))
        {
            return value["/bff/".Length..].TrimStart('/');
        }

        return value.Equals("/bff", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value.TrimStart('/');
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
}
