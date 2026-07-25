using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Plume.Tests.Fixtures;
using Xunit;

namespace Plume.Tests.Pages;

[Collection("PlumeApp")]
public sealed class PlayerAnonymousTests
{
    private readonly PlumeWebApplicationFactory _factory;

    public PlayerAnonymousTests(PlumeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Kithara.ResetAuthScenario();
    }

    [Fact]
    public async Task Player_public_slug_is_anonymous_without_session()
    {
        _factory.Kithara.OpenBySlug = "party";
        _factory.Kithara.OpenPlaybackAccess = "public";

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var response = await client.GetAsync("/player/party");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-plume-island=\"listen\"", html, StringComparison.Ordinal);
        Assert.Contains("data-open-playback=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains(
            _factory.Kithara.Requests,
            r => r.Method == "GET"
                && r.Path.Equals("/api/streams/by-slug/party", StringComparison.OrdinalIgnoreCase)
                && r.Bearer is null);
    }

    [Fact]
    public async Task Player_hidden_slug_is_anonymous_without_session()
    {
        _factory.Kithara.OpenBySlug = "secret-radio";
        _factory.Kithara.OpenPlaybackAccess = "hidden";
        _factory.Kithara.OpenTitle = "Secret";

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/player/secret-radio");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-open-playback=\"true\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Player_unknown_slug_challenges_anonymous()
    {
        _factory.Kithara.OpenBySlug = null;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/player/missing");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Control_still_requires_session()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/control/party");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bff_open_now_playing_works_without_session()
    {
        _factory.Kithara.OpenBySlug = "party";
        _factory.Kithara.OpenNowPlaying = true;

        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/bff/streams/by-slug/party/now-playing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"playing\":true", json, StringComparison.Ordinal);
        Assert.Contains(
            _factory.Kithara.Requests,
            r => r.Method == "GET"
                && r.Path.Equals(
                    "/api/streams/by-slug/party/now-playing",
                    StringComparison.OrdinalIgnoreCase)
                && r.Bearer is null);
    }
}
