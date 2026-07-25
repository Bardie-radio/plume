using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff;

namespace Plume.Pages;

/// <summary>
/// Step 2 — discovery <c>ui_mode</c> drives the UI:
/// <c>form_schema</c> → render fields; <c>redirect</c> → navigate to <c>authorize_url</c>.
/// </summary>
[AllowAnonymous]
public class LoginFormModel(
    IKitharaAuthClient auth,
    IPlumeSessionService sessions) : PageModel
{
    public const string FormSchemaMode = "form_schema";
    public const string RedirectMode = "redirect";

    [BindProperty(SupportsGet = true)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Set only for <see cref="FormSchemaMode"/> (redirect returns before the view).</summary>
    public DiscoveryProvider? Provider { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            return RedirectToPage("/Login");
        }

        var provider = await ResolveProviderAsync(cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return Page();
        }

        return StartProviderUi(provider);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            return RedirectToPage("/Login");
        }

        var provider = await ResolveProviderAsync(cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return Page();
        }

        // Never branch on provider id — only on discovery ui_mode.
        if (string.Equals(provider.UiMode, RedirectMode, StringComparison.OrdinalIgnoreCase))
        {
            return StartRedirect(provider);
        }

        if (!string.Equals(provider.UiMode, FormSchemaMode, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "This sign-in method is not supported yet.";
            return Page();
        }

        Provider = provider;

        var payload = provider.FormFields
            .Where(static f => !string.IsNullOrWhiteSpace(f.Name))
            .ToDictionary(
                static f => f.Name,
                f => Request.Form.TryGetValue($"field_{f.Name}", out var values)
                    ? values.ToString()
                    : string.Empty,
                StringComparer.Ordinal);

        var result = await auth
            .AuthenticateAsync(ProviderId, payload, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.Tokens is null)
        {
            ErrorMessage = result.Error ?? "Sign-in failed.";
            Response.StatusCode = result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                ? (int)result.StatusCode
                : StatusCodes.Status401Unauthorized;
            return Page();
        }

        await sessions.EstablishAsync(HttpContext, result.Tokens, cancellationToken).ConfigureAwait(false);
        return RedirectToPage("/Index");
    }

    private IActionResult StartProviderUi(DiscoveryProvider provider)
    {
        if (string.Equals(provider.UiMode, RedirectMode, StringComparison.OrdinalIgnoreCase))
        {
            return StartRedirect(provider);
        }

        if (string.Equals(provider.UiMode, FormSchemaMode, StringComparison.OrdinalIgnoreCase))
        {
            Provider = provider;
            return Page();
        }

        LoadError = "This sign-in method is not supported yet.";
        return Page();
    }

    private IActionResult StartRedirect(DiscoveryProvider provider)
    {
        // Contracts: navigate to authorize_url; IdP returns to Kithara /api/auth/callback.
        if (string.IsNullOrWhiteSpace(provider.AuthorizeUrl)
            || !Uri.TryCreate(provider.AuthorizeUrl, UriKind.Absolute, out var authorizeUri)
            || (authorizeUri.Scheme != Uri.UriSchemeHttps && authorizeUri.Scheme != Uri.UriSchemeHttp))
        {
            LoadError = "Sign-in redirect is misconfigured.";
            return Page();
        }

        return Redirect(authorizeUri.ToString());
    }

    private async Task<DiscoveryProvider?> ResolveProviderAsync(CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var provider = discovery?.Providers?.FirstOrDefault(p =>
            string.Equals(p.Id, ProviderId, StringComparison.Ordinal));

        if (provider is null)
        {
            LoadError = "Unknown sign-in method.";
        }

        return provider;
    }
}
