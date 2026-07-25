using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Plume.Features.Bff;
using Plume.Features.Bff.KitharaClients;
using Plume.Tests.Fixtures;
using Xunit;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Plume.Tests.Bff;

[Collection("PlumeApp")]
public sealed class AuthInviteTests
{
    private readonly PlumeWebApplicationFactory _factory;

    public AuthInviteTests(PlumeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Kithara.ResetAuthScenario();
    }

    [Fact]
    public async Task Claim_establishes_claim_session_without_tokens_in_body()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var response = await PostJsonWithCsrfAsync(
            client,
            "/bff/auth/claim",
            new
            {
                username = "invitee",
                registration_password = "REG-OTP",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("must_complete_binding").GetBoolean());
        Assert.False(doc.RootElement.TryGetProperty("access_token", out _));

        var sid = Assert.Single(ParseSetCookies(response), c => c.Name == "plume.sid");
        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"{sid.Name}={sid.Value}";
        var stored = await sessions.TryGetAsync(http);
        Assert.NotNull(stored);
        Assert.Equal(KitharaAuthConstants.ClaimProviderId, stored.ProviderId);
        Assert.Equal("access-claim", stored.AccessToken);
    }

    [Fact]
    public async Task Register_with_claim_session_is_forbidden()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await EstablishClaimSessionAsync(client);

        using var response = await PostJsonWithCsrfAsync(
            client,
            "/bff/auth/register",
            new { username = "another" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("must_complete_binding", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bind_with_claim_session_clears_session()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var sidValue = await EstablishClaimSessionAsync(client);

        using var response = await PostJsonWithCsrfAsync(
            client,
            "/bff/auth/bindings/bes",
            new
            {
                ceremony = "bind",
                payload = new Dictionary<string, string> { ["password"] = "secret" },
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("must_complete_binding").GetBoolean());

        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"plume.sid={sidValue}";
        Assert.Null(await sessions.TryGetAsync(http));
    }

    private async Task<string> EstablishClaimSessionAsync(HttpClient client)
    {
        using var response = await PostJsonWithCsrfAsync(
            client,
            "/bff/auth/claim",
            new
            {
                username = "invitee",
                registration_password = "REG-OTP",
            });
        response.EnsureSuccessStatusCode();
        var sid = Assert.Single(ParseSetCookies(response), c => c.Name == "plume.sid");
        Assert.False(string.IsNullOrEmpty(sid.Value.Value));
        return sid.Value.Value!;
    }

    private static async Task<HttpResponseMessage> PostJsonWithCsrfAsync(
        HttpClient client,
        string path,
        object body)
    {
        using var csrfResponse = await client.GetAsync("/bff/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var csrfJson = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = csrfJson.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(BffAntiforgeryMiddleware.HeaderName, token);
        return await client.SendAsync(request);
    }

    private static IList<SetCookieHeaderValue> ParseSetCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
        {
            return [];
        }

        return SetCookieHeaderValue.ParseList(values.ToList());
    }
}
