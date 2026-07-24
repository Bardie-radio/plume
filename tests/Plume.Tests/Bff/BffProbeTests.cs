using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Plume.Features.Bff;
using Xunit;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Plume.Tests.Bff;

public sealed class BffProbeTests : IClassFixture<PlumeWebApplicationFactory>
{
    private readonly PlumeWebApplicationFactory _factory;

    public BffProbeTests(PlumeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Kithara.Requests.Clear();
        _factory.Kithara.ResetAuthMeHits();
        _factory.Kithara.RequireRefreshOnFirstAuthMe = false;
        _factory.Kithara.OmitRotatedRefreshToken = false;
        _factory.Kithara.AccessToken = "access-old";
        _factory.Kithara.RefreshToken = "refresh-old";
        _factory.Kithara.RotatedAccessToken = "access-new";
        _factory.Kithara.RotatedRefreshToken = "refresh-new";
    }

    [Fact]
    public async Task EstablishAsync_sets_httpOnly_session_cookie()
    {
        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();

        await sessions.EstablishAsync(
            http,
            new SessionTokens("access-old", "refresh-old", "bes"));

        var setCookies = ParseSetCookies(http.Response.Headers);
        var sid = Assert.Single(setCookies, c => c.Name == "plume.sid");
        Assert.True(sid.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, sid.SameSite);
        Assert.DoesNotContain("access-old", sid.Value.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-old", sid.Value.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthMe_probe_sends_bearer_and_keeps_tokens_out_of_set_cookie()
    {
        var client = _factory.CreateClient();
        await SeedSessionAsync(client, new SessionTokens("access-old", "refresh-old", "bes"));

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("user-1", body, StringComparison.Ordinal);

        var authMe = Assert.Single(
            _factory.Kithara.Requests,
            r => r.Method == "GET" && r.Path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("access-old", authMe.Bearer);

        AssertNoTokenLeakInSetCookie(response, "access-old", "refresh-old");
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

        var client = _factory.CreateClient();
        var cookieHeader = await SeedSessionAsync(
            client,
            new SessionTokens("access-old", "refresh-old", "bes"));

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Collection(
            _factory.Kithara.Requests,
            r =>
            {
                Assert.Equal("GET", r.Method);
                Assert.Equal("/api/auth/me", r.Path, ignoreCase: true);
                Assert.Equal("access-old", r.Bearer);
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
                Assert.Equal("access-new", r.Bearer);
            });

        AssertNoTokenLeakInSetCookie(response, "access-old", "refresh-old", "access-new", "refresh-new");

        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = cookieHeader;
        var stored = await sessions.TryGetAsync(http);

        Assert.NotNull(stored);
        Assert.Equal("access-new", stored.AccessToken);
        Assert.Equal("refresh-new", stored.RefreshToken);
        Assert.Equal("bes", stored.ProviderId);
    }

    [Fact]
    public async Task AuthMe_on_upstream_401_keeps_prior_refresh_when_omitted()
    {
        _factory.Kithara.RequireRefreshOnFirstAuthMe = true;
        _factory.Kithara.OmitRotatedRefreshToken = true;

        var client = _factory.CreateClient();
        var cookieHeader = await SeedSessionAsync(
            client,
            new SessionTokens("access-old", "refresh-old", "bes"));

        using var response = await client.GetAsync("/bff/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = cookieHeader;
        var stored = await sessions.TryGetAsync(http);

        Assert.NotNull(stored);
        Assert.Equal("access-new", stored.AccessToken);
        Assert.Equal("refresh-old", stored.RefreshToken);
        Assert.Equal("bes", stored.ProviderId);
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
