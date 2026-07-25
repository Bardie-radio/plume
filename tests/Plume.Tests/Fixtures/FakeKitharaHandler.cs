using System.Net;
using System.Text;
using System.Text.Json;

namespace Plume.Tests.Fixtures;

/// <summary>
/// Test-only Kithara HTTP stand-in. Lives under <c>tests/</c> — never part of the Plume app publish.
/// </summary>
public sealed class FakeKitharaHandler : HttpMessageHandler
{
    private int _authMeHits;

    public string AccessToken { get; set; } = "access-old";
    public string RefreshToken { get; set; } = "refresh-old";
    public string RotatedAccessToken { get; set; } = "access-new";
    public string RotatedRefreshToken { get; set; } = "refresh-new";
    public string ProviderId { get; set; } = "bes";

    public string MintedAccessToken { get; set; } = "access-minted";
    public string MintedRefreshToken { get; set; } = "refresh-minted";

    /// <summary>When false, <c>/api/auth/authenticate</c> returns 401 with an error body.</summary>
    public bool AuthenticateSucceeds { get; set; } = true;

    /// <summary>When false, guest exchange returns 401.</summary>
    public bool GuestExchangeSucceeds { get; set; } = true;

    public string ExpectedGuestCode { get; set; } = "ABCD12";

    public string GuestAccessToken { get; set; } = "access-guest";
    public string GuestRefreshToken { get; set; } = "refresh-guest";

    public string AuthenticateError { get; set; } = "invalid credentials";

    /// <summary>When true, first <c>/api/auth/me</c> returns 401 so the BFF must refresh and retry.</summary>
    public bool RequireRefreshOnFirstAuthMe { get; set; }

    /// <summary>When true, refresh JSON omits <c>refresh_token</c> (non-rotated providers).</summary>
    public bool OmitRotatedRefreshToken { get; set; }

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Resets auth/me hit counting so refresh tests are not order-dependent.</summary>
    public void ResetAuthMeHits() => _authMeHits = 0;

    /// <summary>Open-playback by-slug responses (public | hidden). Null slug → 404.</summary>
    public string? OpenBySlug { get; set; } = "party";

    public string OpenPlaybackAccess { get; set; } = "public";

    public Guid OpenStrunaId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public string OpenTitle { get; set; } = "Party";

    /// <summary>When true, by-slug now-playing returns a playing payload.</summary>
    public bool OpenNowPlaying { get; set; }

    public void ResetAuthScenario()
    {
        Requests.Clear();
        ResetAuthMeHits();
        RequireRefreshOnFirstAuthMe = false;
        OmitRotatedRefreshToken = false;
        AuthenticateSucceeds = true;
        GuestExchangeSucceeds = true;
        ExpectedGuestCode = "ABCD12";
        GuestAccessToken = "access-guest";
        GuestRefreshToken = "refresh-guest";
        AccessToken = "access-old";
        RefreshToken = "refresh-old";
        RotatedAccessToken = "access-new";
        RotatedRefreshToken = "refresh-new";
        MintedAccessToken = "access-minted";
        MintedRefreshToken = "refresh-minted";
        AuthenticateError = "invalid credentials";
        ProviderId = "bes";
        OpenBySlug = "party";
        OpenPlaybackAccess = "public";
        OpenStrunaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        OpenTitle = "Party";
        OpenNowPlaying = false;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var bearer = request.Headers.Authorization?.Parameter;
        var contentType = request.Content?.Headers.ContentType?.ToString();
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        Requests.Add(new RecordedRequest(request.Method.Method, path, bearer, contentType, body));

        if (path.Equals("/api/auth/discovery", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Get)
        {
            return HandleDiscovery();
        }

        if (path.Equals("/api/auth/authenticate", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Post)
        {
            return await HandleAuthenticateAsync(body).ConfigureAwait(false);
        }

        if (path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Get)
        {
            return HandleAuthMe(bearer);
        }

        if (path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Post)
        {
            return await HandleRefreshAsync(body).ConfigureAwait(false);
        }

        if (request.Method == HttpMethod.Post
            && path.Contains("/guest/exchange", StringComparison.OrdinalIgnoreCase)
            && path.StartsWith("/api/streams/", StringComparison.OrdinalIgnoreCase))
        {
            return HandleGuestExchange(body);
        }

        if (request.Method == HttpMethod.Get
            && path.StartsWith("/api/streams/by-slug/", StringComparison.OrdinalIgnoreCase))
        {
            return HandleOpenBySlug(path);
        }

        // Catch-all JSON mutations (play / queue / …) — assert Content-Type in proxy tests.
        if (request.Method == HttpMethod.Post
            && path.StartsWith("/api/streams/", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(contentType)
                || contentType.Contains(',', StringComparison.Ordinal)
                || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType)
                {
                    Content = JsonContent("""{"error":"unsupported_media_type"}"""),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"track_job_id":"00000000-0000-0000-0000-000000000001"}"""),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"unexpected path: {path}"),
        };
    }

    private HttpResponseMessage HandleDiscovery()
    {
        var body = JsonSerializer.Serialize(new
        {
            providers = new[]
            {
                new
                {
                    id = ProviderId,
                    display_name = "Bes",
                    module = ProviderId,
                    ui_mode = "login_form",
                    login_form = new[]
                    {
                        new
                        {
                            name = "username",
                            label = "Username",
                            input_type = "text",
                            required = true,
                        },
                        new
                        {
                            name = "password",
                            label = "Password",
                            input_type = "password",
                            required = true,
                        },
                    },
                    form_fields = new[]
                    {
                        new
                        {
                            name = "username",
                            label = "Username",
                            input_type = "text",
                            required = true,
                        },
                        new
                        {
                            name = "password",
                            label = "Password",
                            input_type = "password",
                            required = true,
                        },
                    },
                    bind_form = new[]
                    {
                        new
                        {
                            name = "username",
                            label = "Username",
                            input_type = "text",
                            required = false,
                        },
                        new
                        {
                            name = "password",
                            label = "Current password",
                            input_type = "password",
                            required = true,
                        },
                        new
                        {
                            name = "new_password",
                            label = "New password",
                            input_type = "password",
                            required = false,
                        },
                    },
                    authorize_url = (string?)null,
                },
            },
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private Task<HttpResponseMessage> HandleAuthenticateAsync(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent("{\"error\":\"missing body\"}"),
            });
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var providerId = root.TryGetProperty("provider_id", out var p) ? p.GetString() : null;

        if (!string.Equals(providerId, ProviderId, StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent("""{"error":"unknown provider"}"""),
            });
        }

        if (!AuthenticateSucceeds)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent($"{{\"error\":{JsonSerializer.Serialize(AuthenticateError)}}}"),
            });
        }

        AccessToken = MintedAccessToken;
        RefreshToken = MintedRefreshToken;

        var body = JsonSerializer.Serialize(new
        {
            access_token = MintedAccessToken,
            refresh_token = MintedRefreshToken,
            token_type = "Bearer",
            expires_in = 3600,
        });

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
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

    private Task<HttpResponseMessage> HandleRefreshAsync(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var providerId = root.TryGetProperty("provider_id", out var p) ? p.GetString() : null;
        var refreshToken = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;

        if (!string.Equals(providerId, ProviderId, StringComparison.Ordinal)
            || !string.Equals(refreshToken, RefreshToken, StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
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

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    private HttpResponseMessage HandleGuestExchange(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent("""{"error":"guest_code is required."}"""),
            };
        }

        using var doc = JsonDocument.Parse(json);
        var code = doc.RootElement.TryGetProperty("guest_code", out var c)
            ? c.GetString()
            : doc.RootElement.TryGetProperty("code", out var c2) ? c2.GetString() : null;

        if (!GuestExchangeSucceeds
            || !string.Equals(code, ExpectedGuestCode, StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent("""{"error":"invalid_guest_code"}"""),
            };
        }

        AccessToken = GuestAccessToken;
        RefreshToken = GuestRefreshToken;
        ProviderId = "kithara.guest";

        var body = JsonSerializer.Serialize(new
        {
            access_token = GuestAccessToken,
            refresh_token = GuestRefreshToken,
            token_type = "Bearer",
            expires_in = 3600,
            user_id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private HttpResponseMessage HandleOpenBySlug(string path)
    {
        // /api/streams/by-slug/{slug} or .../now-playing
        const string prefix = "/api/streams/by-slug/";
        var rest = path[prefix.Length..];
        var slash = rest.IndexOf('/');
        var slug = slash < 0 ? rest : rest[..slash];
        var suffix = slash < 0 ? string.Empty : rest[slash..];

        if (string.IsNullOrWhiteSpace(OpenBySlug)
            || !string.Equals(slug, OpenBySlug, StringComparison.OrdinalIgnoreCase))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent("""{"error":"not_found"}"""),
            };
        }

        if (suffix.Equals("/now-playing", StringComparison.OrdinalIgnoreCase))
        {
            var np = OpenNowPlaying
                ? """{"playing":true,"paused":false,"title":"Never","artist":"Rick","stream_title":"Rick - Never"}"""
                : """{"playing":false}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(np),
            };
        }

        if (!string.IsNullOrEmpty(suffix))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"unexpected path: {path}"),
            };
        }

        var meta = JsonSerializer.Serialize(new
        {
            id = OpenStrunaId,
            slug = OpenBySlug,
            title = OpenTitle,
            playback_access = OpenPlaybackAccess,
            control_access = "private",
            owner_user_id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            created_at = DateTimeOffset.UtcNow,
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(meta, Encoding.UTF8, "application/json"),
        };
    }

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    public sealed record RecordedRequest(
        string Method,
        string Path,
        string? Bearer,
        string? ContentType = null,
        string? Body = null);
}
