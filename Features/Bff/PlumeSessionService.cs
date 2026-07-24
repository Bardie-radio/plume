using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

public sealed class PlumeSessionService(
    ISessionTokenStore store,
    IOptions<SessionOptions> sessionOptions) : IPlumeSessionService
{
    private readonly SessionOptions _options = sessionOptions.Value;

    public Task EstablishAsync(
        HttpContext http,
        SessionTokens tokens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokens.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokens.RefreshToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokens.ProviderId);

        // Anti-fixation: drop any prior session bound to this browser cookie.
        if (http.Request.Cookies.TryGetValue(_options.CookieName, out var priorId)
            && !string.IsNullOrWhiteSpace(priorId))
        {
            store.Remove(priorId);
        }

        var sessionId = CreateSessionId();
        store.Set(sessionId, tokens);
        http.Response.Cookies.Append(_options.CookieName, sessionId, BuildCookieOptions(http));
        return Task.CompletedTask;
    }

    public Task ClearAsync(HttpContext http, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (http.Request.Cookies.TryGetValue(_options.CookieName, out var sessionId)
            && !string.IsNullOrWhiteSpace(sessionId))
        {
            store.Remove(sessionId);
        }

        http.Response.Cookies.Delete(_options.CookieName, BuildCookieOptions(http));
        return Task.CompletedTask;
    }

    public Task<SessionTokens?> TryGetAsync(
        HttpContext http,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (!http.Request.Cookies.TryGetValue(_options.CookieName, out var sessionId)
            || string.IsNullOrWhiteSpace(sessionId)
            || !store.TryGet(sessionId, out var tokens))
        {
            return Task.FromResult<SessionTokens?>(null);
        }

        return Task.FromResult<SessionTokens?>(tokens);
    }

    public bool TryUpdateTokens(HttpContext http, SessionTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(tokens);

        if (!http.Request.Cookies.TryGetValue(_options.CookieName, out var sessionId)
            || string.IsNullOrWhiteSpace(sessionId)
            || !store.TryGet(sessionId, out _))
        {
            return false;
        }

        store.Set(sessionId, tokens);
        return true;
    }

    private CookieOptions BuildCookieOptions(HttpContext http) =>
        new()
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = _options.SameSiteMode,
            Path = "/",
            IsEssential = true,
        };

    private static string CreateSessionId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
