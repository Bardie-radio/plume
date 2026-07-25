using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Plume.Features.Bff;
using Plume.Tests.Fixtures;
using Xunit;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Plume.Tests.Bff;

[Collection("PlumeApp")]
public sealed class BffProbeTests
{
    private readonly PlumeWebApplicationFactory _factory;

    public BffProbeTests(PlumeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Kithara.ResetAuthScenario();
    }

    [Fact]
    public async Task EstablishAsync_sets_httpOnly_session_cookie()
    {
        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();

        await sessions.EstablishAsync(
            http,
            new SessionTokens(_factory.Kithara.AccessToken, _factory.Kithara.RefreshToken, "bes"));

        var setCookies = ParseSetCookies(http.Response.Headers);
        var sid = Assert.Single(setCookies, c => c.Name == "plume.sid");
        Assert.True(sid.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, sid.SameSite);
        Assert.DoesNotContain(_factory.Kithara.AccessToken, sid.Value.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-old", sid.Value.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthMe_probe_sends_bearer_and_keeps_tokens_out_of_set_cookie()
    {
        var client = _factory.CreateClient();
        await SeedSessionAsync(client, new SessionTokens(_factory.Kithara.AccessToken, _factory.Kithara.RefreshToken, "bes"));

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("user-1", body, StringComparison.Ordinal);

        var authMe = Assert.Single(
            _factory.Kithara.Requests,
            r => r.Method == "GET" && r.Path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(_factory.Kithara.AccessToken, authMe.Bearer);

        AssertNoTokenLeakInSetCookie(response, _factory.Kithara.AccessToken, _factory.Kithara.RefreshToken);
    }

    [Fact]
    public async Task AuthMe_without_session_returns_401()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Kithara.Requests);
    }

    [Fact]
    public async Task AuthMe_on_upstream_401_refreshes_retries_and_updates_store()
    {
        _factory.Kithara.RequireRefreshOnFirstAuthMe = true;

        var accessBefore = _factory.Kithara.AccessToken;
        var refreshBefore = _factory.Kithara.RefreshToken;
        var accessAfter = _factory.Kithara.RotatedAccessToken;
        var refreshAfter = _factory.Kithara.RotatedRefreshToken;

        var client = _factory.CreateClient();
        var cookieHeader = await SeedSessionAsync(
            client,
            new SessionTokens(accessBefore, refreshBefore, "bes"));

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Collection(
            _factory.Kithara.Requests,
            r =>
            {
                Assert.Equal("GET", r.Method);
                Assert.Equal("/api/auth/me", r.Path, ignoreCase: true);
                Assert.Equal(accessBefore, r.Bearer);
            },
            r =>
            {
                Assert.Equal("POST", r.Method);
                Assert.Equal("/api/auth/refresh", r.Path, ignoreCase: true);
            },
            r =>
            {
                Assert.Equal("GET", r.Method);
                Assert.Equal("/api/auth/me", r.Path, ignoreCase: true);
                Assert.Equal(accessAfter, r.Bearer);
            });

        AssertNoTokenLeakInSetCookie(response, accessBefore, refreshBefore, accessAfter, refreshAfter);

        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = cookieHeader;
        var stored = await sessions.TryGetAsync(http);

        Assert.NotNull(stored);
        Assert.Equal(accessAfter, stored.AccessToken);
        Assert.Equal(refreshAfter, stored.RefreshToken);
        Assert.Equal("bes", stored.ProviderId);
    }

    [Fact]
    public async Task AuthMe_on_upstream_401_keeps_prior_refresh_when_omitted()
    {
        _factory.Kithara.RequireRefreshOnFirstAuthMe = true;
        _factory.Kithara.OmitRotatedRefreshToken = true;

        var accessBefore = _factory.Kithara.AccessToken;
        var refreshBefore = _factory.Kithara.RefreshToken;
        var accessAfter = _factory.Kithara.RotatedAccessToken;

        var client = _factory.CreateClient();
        var cookieHeader = await SeedSessionAsync(
            client,
            new SessionTokens(accessBefore, refreshBefore, "bes"));

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = cookieHeader;
        var stored = await sessions.TryGetAsync(http);

        Assert.NotNull(stored);
        Assert.Equal(accessAfter, stored.AccessToken);
        Assert.Equal(refreshBefore, stored.RefreshToken);
        Assert.Equal("bes", stored.ProviderId);
    }

    [Fact]
    public async Task Proxy_post_json_forwards_single_application_json_content_type()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        await SeedSessionAsync(client, new SessionTokens(_factory.Kithara.AccessToken, _factory.Kithara.RefreshToken, "bes"));

        using var csrfResponse = await client.GetAsync("/bff/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var csrfJson = await csrfResponse.Content.ReadAsStringAsync();
        using var csrfDoc = System.Text.Json.JsonDocument.Parse(csrfJson);
        var token = csrfDoc.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var content = new StringContent(
            """{"search_result_id":"11111111-1111-1111-1111-111111111111"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/bff/streams/9507f88e-e5a9-4833-8b4a-025e18b3e80b/play")
        {
            Content = content,
        };
        request.Headers.Add(BffAntiforgeryMiddleware.HeaderName, token);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var play = Assert.Single(
            _factory.Kithara.Requests,
            r => r.Method == "POST"
                && r.Path.EndsWith("/play", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(_factory.Kithara.AccessToken, play.Bearer);
        Assert.NotNull(play.ContentType);
        Assert.StartsWith("application/json", play.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(',', play.ContentType);
        Assert.Contains("search_result_id", play.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_include_content_security_policy()
    {
        var client = _factory.CreateClient();
        using var response = await client.GetAsync("/bff/auth/discovery");
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var csp = string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
    }

    private async Task<string> SeedSessionAsync(HttpClient client, SessionTokens tokens)
    {
        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        await sessions.EstablishAsync(http, tokens);

        var sid = Assert.Single(ParseSetCookies(http.Response.Headers), c => c.Name == "plume.sid");
        var cookieHeader = $"{sid.Name}={sid.Value}";
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        return cookieHeader;
    }

    private static void AssertNoTokenLeakInSetCookie(HttpResponseMessage response, params string[] tokens)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return;
        }

        var joined = string.Join('\n', values);
        foreach (var token in tokens)
        {
            Assert.DoesNotContain(token, joined, StringComparison.Ordinal);
        }
    }

    private static IList<SetCookieHeaderValue> ParseSetCookies(IHeaderDictionary headers) =>
        SetCookieHeaderValue.ParseList(
            headers.SetCookie.Where(v => v is not null).Cast<string>().ToList());
}
