using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Features.Bff;

public static class PlumeSessionDefaults
{
    public const string AuthenticationScheme = "PlumeSession";
}

/// <summary>
/// Maps a valid BFF session cookie to <see cref="ClaimsPrincipal"/>.
/// Access + refresh JWTs stay in the server store; claims are copied from the access token
/// onto <see cref="HttpContext.User"/> for Razor authorization (PLUME-AUTH-001).
/// </summary>
public sealed class PlumeSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPlumeSessionService sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tokens = await sessions.TryGetAsync(Context, Context.RequestAborted).ConfigureAwait(false);
        if (tokens is null)
        {
            return AuthenticateResult.NoResult();
        }

        var claims = new List<Claim>(AccessTokenClaims.ReadUnvalidated(tokens.AccessToken));

        // Session store provider is authoritative for refresh routing (may disagree with stale JWT).
        claims.RemoveAll(static c =>
            string.Equals(c.Type, "bardie_provider", StringComparison.Ordinal));
        claims.Add(new Claim("bardie_provider", tokens.ProviderId));

        if (string.Equals(
                tokens.ProviderId,
                KitharaAuthConstants.ClaimProviderId,
                StringComparison.Ordinal))
        {
            claims.RemoveAll(static c => c.Type == ClaimTypes.Role);
            if (!claims.Exists(static c =>
                    string.Equals(c.Type, ClaimInviteBindOnly, StringComparison.Ordinal)))
            {
                claims.Add(new Claim(ClaimInviteBindOnly, "true"));
            }
        }

        var identity = new ClaimsIdentity(
            claims,
            Scheme.Name,
            ClaimTypes.Name,
            ClaimTypes.Role);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private const string ClaimInviteBindOnly = "bardie_bind_only";

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Razor pages: send anonymous users to discovery login, keeping the path so
        // /control/{slug} can prefill the guest-join slug field.
        var path = Request.Path.HasValue ? Request.Path.Value! : "/";
        var query = Request.QueryString.HasValue ? Request.QueryString.Value! : string.Empty;
        var returnUrl = path + query;
        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            Response.Redirect("/login");
            return Task.CompletedTask;
        }

        Response.Redirect($"/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    }
}
