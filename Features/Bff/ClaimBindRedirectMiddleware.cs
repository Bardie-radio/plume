using Plume.Features.Bff.KitharaClients;

namespace Plume.Features.Bff;

/// <summary>
/// Claim sessions may only complete registration — redirect Desk / home / account away from product UI.
/// </summary>
public sealed class ClaimBindRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldRedirect(context))
        {
            context.Response.Redirect("/claim/bind");
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool ShouldRedirect(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var provider = user.FindFirst("bardie_provider")?.Value;
        if (!string.Equals(provider, KitharaAuthConstants.ClaimProviderId, StringComparison.Ordinal))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/claim", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/bff", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/dist", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
