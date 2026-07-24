using System.Net;
using System.Text;
using System.Text.Json;

namespace Plume.Tests.Bff;

/// <summary>In-memory stand-in for Kithara HTTP used by BFF integration tests.</summary>
public sealed class FakeKitharaHandler : HttpMessageHandler
{
    private int _authMeHits;

    public string AccessToken { get; set; } = "access-old";
    public string RefreshToken { get; set; } = "refresh-old";
    public string RotatedAccessToken { get; set; } = "access-new";
    public string RotatedRefreshToken { get; set; } = "refresh-new";
    public string ProviderId { get; set; } = "bes";

    /// <summary>When true, first <c>/api/auth/me</c> returns 401 so the BFF must refresh and retry.</summary>
    public bool RequireRefreshOnFirstAuthMe { get; set; }

    /// <summary>When true, refresh JSON omits <c>refresh_token</c> (non-rotated providers).</summary>
    public bool OmitRotatedRefreshToken { get; set; }

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Resets auth/me hit counting so refresh tests are not order-dependent.</summary>
    public void ResetAuthMeHits() => _authMeHits = 0;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var bearer = request.Headers.Authorization?.Parameter;
        Requests.Add(new RecordedRequest(request.Method.Method, path, bearer));

        if (path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Get)
        {
            return Task.FromResult(HandleAuthMe(bearer));
        }

        if (path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Post)
        {
            return HandleRefreshAsync(request, cancellationToken);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"unexpected path: {path}"),
        });
    }

    private HttpResponseMessage HandleAuthMe(string? bearer)
    {
        _authMeHits++;

        if (RequireRefreshOnFirstAuthMe && _authMeHits == 1)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (!string.Equals(bearer, AccessToken, StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"sub":"user-1"}""",
                Encoding.UTF8,
                "application/json"),
        };
    }

    private async Task<HttpResponseMessage> HandleRefreshAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        var json = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var providerId = root.TryGetProperty("provider_id", out var p) ? p.GetString() : null;
        var refreshToken = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;

        if (!string.Equals(providerId, ProviderId, StringComparison.Ordinal)
            || !string.Equals(refreshToken, RefreshToken, StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        AccessToken = RotatedAccessToken;
        if (!OmitRotatedRefreshToken)
        {
            RefreshToken = RotatedRefreshToken;
        }

        object payload = OmitRotatedRefreshToken
            ? new { access_token = RotatedAccessToken }
            : new { access_token = RotatedAccessToken, refresh_token = RotatedRefreshToken };

        var body = JsonSerializer.Serialize(payload);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    public sealed record RecordedRequest(string Method, string Path, string? Bearer);
}
