using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Account;

/// <summary>Pick a provider to edit binding data (discovery providers with <c>bind_form</c>).</summary>
[Authorize]
public class CredentialsModel(IKitharaAuthClient auth) : PageModel
{
    public IReadOnlyList<DiscoveryProvider> Providers { get; private set; } = [];

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        if (discovery?.Providers is null || discovery.Providers.Count == 0)
        {
            LoadError = "Account options are unavailable. Try again later.";
            Providers = [];
            return Page();
        }

        Providers = discovery.Providers
            .Where(static p => p.BindForm is { Count: > 0 })
            .ToList();

        if (Providers.Count == 0)
        {
            LoadError = "No providers support account updates.";
        }

        return Page();
    }
}
