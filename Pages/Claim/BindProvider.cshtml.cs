using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Claim;

[Authorize]
public class BindProviderModel(
    IKitharaAuthClient auth,
    IPlumeSessionService sessions) : PageModel
{
    public const string RegistrationCompleteMessage = "RegistrationCompleteMessage";

    [BindProperty(SupportsGet = true)]
    public string ProviderId { get; set; } = string.Empty;

    public DiscoveryProvider? Provider { get; private set; }

    public string ProviderDisplayName { get; private set; } = string.Empty;

    public IReadOnlyList<DiscoveryFormField> BindFields { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            return RedirectToPage("/Claim/Bind");
        }

        var provider = await ResolveProviderAsync(cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return Page();
        }

        if (provider.BindForm is null || provider.BindForm.Count == 0)
        {
            LoadError = "This provider does not support binding.";
            return Page();
        }

        ApplyProvider(provider);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            return RedirectToPage("/Claim/Bind");
        }

        var provider = await ResolveProviderAsync(cancellationToken).ConfigureAwait(false);
        if (provider?.BindForm is null || provider.BindForm.Count == 0)
        {
            LoadError = "This provider does not support binding.";
            return Page();
        }

        ApplyProvider(provider);

        var payload = BindFields
            .Where(static f => !string.IsNullOrWhiteSpace(f.Name))
            .ToDictionary(
                static f => f.Name,
                f => Request.Form.TryGetValue($"field_{f.Name}", out var values)
                    ? values.ToString()
                    : string.Empty,
                StringComparer.Ordinal);

        var result = await auth
            .UpdateBindingAsync(HttpContext, ProviderId, payload, ceremony: "bind", cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            ErrorMessage = result.Error ?? "Binding failed.";
            Response.StatusCode = result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                or HttpStatusCode.Forbidden
                ? (int)result.StatusCode
                : StatusCodes.Status400BadRequest;
            return Page();
        }

        await sessions.ClearAsync(HttpContext, cancellationToken).ConfigureAwait(false);
        TempData[RegistrationCompleteMessage] =
            "Registration is complete. Sign in with the method you just linked.";
        return RedirectToPage("/Login");
    }

    private void ApplyProvider(DiscoveryProvider provider)
    {
        Provider = provider;
        ProviderDisplayName = string.IsNullOrWhiteSpace(provider.DisplayName)
            ? provider.Id
            : provider.DisplayName.Trim();
        BindFields = provider.BindForm!
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
}
