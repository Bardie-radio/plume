using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Plume.Features.Bff;
using Plume.Tests.Fixtures;
using Xunit;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Plume.Tests.Bff;

[Collection("PlumeApp")]
public sealed class AuthLoginTests
{
    private readonly PlumeWebApplicationFactory _factory;

    public AuthLoginTests(PlumeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Kithara.ResetAuthScenario();
    }

    [Fact]
    public async Task Discovery_without_session_returns_form_schema_providers()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/bff/auth/discovery");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("form_schema", json, StringComparison.Ordinal);
        Assert.Contains("form_fields", json, StringComparison.Ordinal);
        Assert.Contains("username", json, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", json, StringComparison.Ordinal);

        Assert.Contains(
            _factory.Kithara.Requests,
            r => r.Method == "GET" && r.Path.Equals("/api/auth/discovery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_establishes_httpOnly_cookie_without_tokens_in_body()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var response = await client.PostAsJsonAsync(
            "/bff/auth/login",
            new
            {
                provider_id = "bes",
                payload = new Dictionary<string, string>
                {
                    ["username"] = "admin",
                    ["password"] = "secret",
                },
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(doc.RootElement.TryGetProperty("access_token", out _));
        Assert.False(doc.RootElement.TryGetProperty("refresh_token", out _));
        Assert.DoesNotContain("access-minted", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-minted", body, StringComparison.Ordinal);

        var sid = Assert.Single(
            ParseSetCookies(response),
            c => c.Name == "plume.sid");
        Assert.True(sid.HttpOnly);
        Assert.DoesNotContain("access-minted", sid.Value.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-minted", sid.Value.Value, StringComparison.Ordinal);

        Assert.Contains(
            _factory.Kithara.Requests,
            r => r.Method == "POST"
                && r.Path.Equals("/api/auth/authenticate", StringComparison.OrdinalIgnoreCase));

        // Session is usable for proxied /auth/me.
        using var me = await client.GetAsync("/bff/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_failure_returns_error_without_tokens_or_session()
    {
        _factory.Kithara.AuthenticateSucceeds = false;
        _factory.Kithara.AuthenticateError = "invalid credentials";

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var response = await client.PostAsJsonAsync(
            "/bff/auth/login",
            new
            {
                provider_id = "bes",
                payload = new Dictionary<string, string>
                {
                    ["username"] = "admin",
                    ["password"] = "wrong",
                },
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid credentials", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh_token", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access-minted", body, StringComparison.Ordinal);

        Assert.DoesNotContain(
            ParseSetCookies(response),
            c => c.Name == "plume.sid");

        using var me = await client.GetAsync("/bff/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Proxy_does_not_expose_authenticate_or_refresh()
    {
        var client = _factory.CreateClient();
        await SeedSessionAsync(client, new SessionTokens("access-old", "refresh-old", "bes"));
        _factory.Kithara.Requests.Clear();

        using var authenticate = await client.PostAsJsonAsync(
            "/bff/auth/authenticate",
            new { provider_id = "bes", payload = new { username = "x" } });
        using var refresh = await client.PostAsJsonAsync(
            "/bff/auth/refresh",
            new { provider_id = "bes", refresh_token = "refresh-old" });

        Assert.Equal(HttpStatusCode.NotFound, authenticate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, refresh.StatusCode);

        var authBody = await authenticate.Content.ReadAsStringAsync();
        var refreshBody = await refresh.Content.ReadAsStringAsync();
        Assert.DoesNotContain("access_token", authBody, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", refreshBody, StringComparison.Ordinal);

        Assert.DoesNotContain(
            _factory.Kithara.Requests,
            r => r.Path.Equals("/api/auth/authenticate", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            _factory.Kithara.Requests,
            r => r.Path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Guest_exchange_is_not_json_proxied()
    {
        var client = _factory.CreateClient();
        await SeedSessionAsync(client, new SessionTokens("access-old", "refresh-old", "bes"));
        _factory.Kithara.Requests.Clear();

        var strunaId = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync(
            $"/bff/streams/{strunaId}/guest/exchange",
            new { guest_code = "ABCD" });

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("access_token", body, StringComparison.Ordinal);
        Assert.Empty(_factory.Kithara.Requests);
    }

    private async Task SeedSessionAsync(HttpClient client, SessionTokens tokens)
    {
        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        await sessions.EstablishAsync(http, tokens);

        var sid = Assert.Single(ParseSetCookies(http.Response.Headers), c => c.Name == "plume.sid");
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"{sid.Name}={sid.Value}");
    }

    private static IList<SetCookieHeaderValue> ParseSetCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return [];
        }

        return SetCookieHeaderValue.ParseList(values.ToList());
    }

    private static IList<SetCookieHeaderValue> ParseSetCookies(IHeaderDictionary headers) =>
        SetCookieHeaderValue.ParseList(
            headers.SetCookie.Where(v => v is not null).Cast<string>().ToList());
}
