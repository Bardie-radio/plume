using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

public static class PlumeSessionDefaults
{
    public const string AuthenticationScheme = "PlumeSession";
}

/// <summary>
/// Maps a valid BFF session cookie to <see cref="ClaimsPrincipal"/>.
/// Tokens stay in the server store — never in claims.
/// PLUME-AUTH-001: gate only today (authenticated + provider id); real JWT claims are backlog.
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

        // PLUME-AUTH-001 — do not invent roles/sub here; map access-token claims later.
        var identity = new ClaimsIdentity(
            [
                new Claim("bardie_provider", tokens.ProviderId),
            ],
            Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

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