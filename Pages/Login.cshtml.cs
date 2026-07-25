using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages;

/// <summary>Step 1 — pick an auth provider from discovery.</summary>
[AllowAnonymous]
public class LoginModel(IKitharaAuthClient auth) : PageModel
{
    public IReadOnlyList<DiscoveryProvider> Providers { get; private set; } = [];

    public string? DiscoveryError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        if (discovery?.Providers is null || discovery.Providers.Count == 0)
        {
            DiscoveryError = "Sign-in options are unavailable. Try again later.";
            Providers = [];
            return Page();
        }

        Providers = discovery.Providers;
        return Page();
    }
}
