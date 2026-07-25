using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages;

/// <summary>
/// Step 1 — pick an auth provider from discovery, or join a protected Struna with a guest code.
/// </summary>
[AllowAnonymous]
public class LoginModel(
    IKitharaAuthClient auth,
    IKitharaGuestClient guests,
    IPlumeSessionService sessions) : PageModel
{
    public IReadOnlyList<DiscoveryProvider> Providers { get; private set; } = [];

    public string? DiscoveryError { get; private set; }

    public string? GuestError { get; private set; }

    /// <summary>Safe relative path to return to after sign-in / guest join (e.g. <c>/control/party</c>).</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string GuestSlug { get; set; } = string.Empty;

    [BindProperty]
    public string GuestCode { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(SafeLocalRedirect(ReturnUrl) ?? "/");
        }

        PrefillGuestSlugFromReturnUrl();
        await LoadDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        return Page();
    }

    public async Task<IActionResult> OnPostGuestAsync(CancellationToken cancellationToken)
    {
        PrefillGuestSlugFromReturnUrl();

        var slug = GuestSlug?.Trim().ToLowerInvariant() ?? string.Empty;
        var code = GuestCode?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(slug))
        {
            GuestError = "Struna slug is required.";
            await LoadDiscoveryAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            GuestError = "Guest code is required.";
            GuestSlug = slug;
            await LoadDiscoveryAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var result = await guests
            .ExchangeBySlugAsync(slug, code, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.Tokens is null)
        {
            GuestError = result.Error ?? "Guest exchange failed.";
            GuestSlug = slug;
            Response.StatusCode = result.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.BadRequest
                or HttpStatusCode.NotFound
                or HttpStatusCode.TooManyRequests
                ? (int)result.StatusCode
                : StatusCodes.Status401Unauthorized;
            await LoadDiscoveryAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        await sessions.EstablishAsync(HttpContext, result.Tokens, cancellationToken).ConfigureAwait(false);

        // Guest form owns the destination: use the slug they submitted, not Challenge ReturnUrl
        // (they may have changed A → B after being redirected from /control/A).
        return Redirect($"/control/{Uri.EscapeDataString(slug)}");
    }

    private async Task LoadDiscoveryAsync(CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        if (discovery?.Providers is null || discovery.Providers.Count == 0)
        {
            DiscoveryError = "Sign-in options are unavailable. Try again later.";
            Providers = [];
            return;
        }

        Providers = discovery.Providers;
    }

    private void PrefillGuestSlugFromReturnUrl()
    {
        if (!string.IsNullOrWhiteSpace(GuestSlug))
        {
            return;
        }

        GuestSlug = TryExtractSlug(ReturnUrl) ?? string.Empty;
    }

    /// <summary>
    /// Pull slug from <c>/control/{slug}</c> or <c>/player/{slug}</c> when Challenge redirected here.
    /// </summary>
    public static string? TryExtractSlug(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
        {
            return null;
        }

        // Strip query/fragment.
        var path = returnUrl.Split('?', 2)[0].Split('#', 2)[0];
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        if (!parts[0].Equals("control", StringComparison.OrdinalIgnoreCase)
            && !parts[0].Equals("player", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = Uri.UnescapeDataString(parts[1]).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    /// <summary>Only relative same-site paths — never open redirects.</summary>
    public static string? SafeLocalRedirect(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !returnUrl.StartsWith('/')
            || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return returnUrl;
    }
}
