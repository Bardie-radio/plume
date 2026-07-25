using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Account;

/// <summary>
/// Forced rotate / voluntary credential change via discovery <c>bind_form</c>
/// → <c>UpdateUserBinding</c> ceremony <c>update</c>.
/// </summary>
[Authorize]
public class CredentialsModel(IKitharaAuthClient auth) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ProviderId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public DiscoveryProvider? Provider { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? LoadError { get; private set; }

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
            LoadError = "This provider does not support credential updates.";
            return Page();
        }

        Provider = provider;
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
            LoadError = "This provider does not support credential updates.";
            return Page();
        }

        Provider = provider;

        var payload = provider.BindForm
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
            ErrorMessage = result.Error ?? "Credential update failed.";
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
