using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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
public sealed class SessionClaimsTests
{
    private readonly PlumeWebApplicationFactory _factory;

    public SessionClaimsTests(PlumeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Kithara.ResetAuthScenario();
    }

    [Fact]
    public void AccessTokenClaims_maps_roles_and_sub()
    {
        var jwt = TestAccessJwt.CreateAdmin(subject: "alice");
        var claims = AccessTokenClaims.ReadUnvalidated(jwt);

        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "alice");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");
        Assert.Contains(claims, c => c.Type == "bardie_provider" && c.Value == "bes");
    }

    [Fact]
    public async Task Invite_requires_admin_role_from_access_jwt()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        await SeedSessionAsync(client, new SessionTokens(TestAccessJwt.CreateUser(), "refresh", "bes"));
        using var forbidden = await client.GetAsync("/account/invite");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await SeedSessionAsync(client, new SessionTokens(TestAccessJwt.CreateAdmin(), "refresh", "bes"));
        _factory.Kithara.AccessToken = TestAccessJwt.CreateAdmin();
        using var ok = await client.GetAsync("/account/invite");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var html = await ok.Content.ReadAsStringAsync();
        Assert.Contains("Invite user", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Claim_session_redirects_desk_to_claim_bind()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        await EstablishClaimSessionAsync(client);

        using var response = await client.GetAsync("/control/party");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/claim/bind", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Claim_session_still_serves_claim_bind()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        await EstablishClaimSessionAsync(client);

        using var response = await client.GetAsync("/claim/bind");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task EstablishClaimSessionAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/bff/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var csrfJson = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = csrfJson.GetProperty("token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/bff/auth/claim")
        {
            Content = JsonContent.Create(new
            {
                username = "invitee",
                registration_password = "REG-OTP",
            }),
        };
        request.Headers.Add(BffAntiforgeryMiddleware.HeaderName, token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task SeedSessionAsync(HttpClient client, SessionTokens tokens)
    {
        var sessions = _factory.Services.GetRequiredService<IPlumeSessionService>();
        var http = new DefaultHttpContext();
        await sessions.EstablishAsync(http, tokens);

        var sid = Assert.Single(
            SetCookieHeaderValue.ParseList(
                http.Response.Headers.SetCookie.Where(v => v is not null).Cast<string>().ToList()),
            c => c.Name == "plume.sid");
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"{sid.Name}={sid.Value}");
    }
}
