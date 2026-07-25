using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Claim;

/// <summary>Pick a provider to bind after claim (discovery providers with bind_form).</summary>
[Authorize]
public class BindModel(IKitharaAuthClient auth) : PageModel
{
    public IReadOnlyList<DiscoveryProvider> Providers { get; private set; } = [];

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        if (discovery?.Providers is null || discovery.Providers.Count == 0)
        {
            LoadError = "Binding options are unavailable. Try again later.";
            Providers = [];
            return Page();
        }

        Providers = discovery.Providers
            .Where(static p => p.BindForm is { Count: > 0 })
            .ToList();

        if (Providers.Count == 0)
        {
            LoadError = "No binding providers are available.";
        }

        return Page();
    }
}
