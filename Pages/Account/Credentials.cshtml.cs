using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Account;

/// <summary>
/// Binding editor: discovery <c>bind_form</c> → <c>UpdateUserBinding</c>.
/// Step-up is a separate <c>Authenticate</c> (login_form modal / redirect) — never merged into the binding bag.
/// </summary>
[Authorize]
public class CredentialsModel(IKitharaAuthClient auth) : PageModel
{
    public const string LoginFormMode = "login_form";
    public const string FormSchemaMode = "form_schema";
    public const string RedirectMode = "redirect";

    [BindProperty(SupportsGet = true)]
    public string ProviderId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public DiscoveryProvider? Provider { get; private set; }

    public string ProviderDisplayName { get; private set; } = string.Empty;

    /// <summary>Full discovery <c>bind_form</c> (module-owned binding data).</summary>
    public IReadOnlyList<DiscoveryFormField> BindFields { get; private set; } = [];

    /// <summary>Discovery <c>login_form</c> for step-up Authenticate when <see cref="UiMode"/> is form-based.</summary>
    public IReadOnlyList<DiscoveryFormField> LoginFields { get; private set; } = [];

    public string UiMode { get; private set; } = string.Empty;

    public string? AuthorizeUrl { get; private set; }

    public bool UsesLoginFormStepUp =>
        IsLoginFormMode(UiMode) && LoginFields.Count > 0;

    public bool UsesRedirectStepUp =>
        string.Equals(UiMode, RedirectMode, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(AuthorizeUrl);

    public string? ErrorMessage { get; private set; }

    public string? LoadError { get; private set; }

    public static bool IsLoginFormMode(string? uiMode) =>
        string.Equals(uiMode, LoginFormMode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uiMode, FormSchemaMode, StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            ProviderId = await ResolveDefaultProviderAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            LoadError = "No binding provider is available.";
            return Page();
        }

        var provider = await ResolveProviderAsync(cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return Page();
        }

        if (provider.BindForm is null || provider.BindForm.Count == 0)
        {
            LoadError = "This provider does not support binding updates.";
            return Page();
        }

        ApplyProvider(provider);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            LoadError = "Provider is required.";
            return Page();
        }

        var provider = await ResolveProviderAsync(cancellationToken).ConfigureAwait(false);
        if (provider?.BindForm is null || provider.BindForm.Count == 0)
        {
            LoadError = "This provider does not support binding updates.";
            return Page();
        }

        ApplyProvider(provider);

        // bind_form only — step-up already ran as Authenticate (BFF login) in the browser.
        var payload = BindFields
            .Where(static f => !string.IsNullOrWhiteSpace(f.Name))
            .ToDictionary(
                static f => f.Name,
                f => Request.Form.TryGetValue($"field_{f.Name}", out var values)
                    ? values.ToString()
                    : string.Empty,
                StringComparer.Ordinal);

        var result = await auth
            .UpdateBindingAsync(HttpContext, ProviderId, payload, ceremony: "update", cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            ErrorMessage = result.Error ?? "Binding update failed.";
            Response.StatusCode = result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                or HttpStatusCode.Forbidden
                ? (int)result.StatusCode
                : StatusCodes.Status400BadRequest;
            return Page();
        }

        if (result.MustRotateCredentials)
        {
            ErrorMessage = "Credentials still require rotation.";
            return Page();
        }

        return Redirect(Pages.LoginModel.SafeLocalRedirect(ReturnUrl) ?? "/");
    }

    private void ApplyProvider(DiscoveryProvider provider)
    {
        Provider = provider;
        ProviderDisplayName = string.IsNullOrWhiteSpace(provider.DisplayName)
            ? provider.Id
            : provider.DisplayName.Trim();
        UiMode = provider.UiMode ?? string.Empty;
        AuthorizeUrl = provider.AuthorizeUrl;
        BindFields = provider.BindForm!
            .Where(static f => !string.IsNullOrWhiteSpace(f.Name))
            .ToArray();
        LoginFields = provider.EffectiveLoginFields
            .Where(static f => !string.IsNullOrWhiteSpace(f.Name))
            .ToArray();
    }

    private async Task<DiscoveryProvider?> ResolveProviderAsync(CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var provider = discovery?.Providers?.FirstOrDefault(p =>
            string.Equals(p.Id, ProviderId, StringComparison.Ordinal));

        if (provider is null)
        {
            LoadError = "Unknown provider.";
        }

        return provider;
    }

    private async Task<string?> ResolveDefaultProviderAsync(CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        return discovery?.Providers?
            .FirstOrDefault(p => p.BindForm is { Count: > 0 })
            ?.Id;
    }
}
