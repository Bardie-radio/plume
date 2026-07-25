using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Control;

[Authorize]
public class IndexModel(IKitharaStreamsClient streams) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    public Guid StrunaId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var slug = Slug?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        Slug = slug;

        var control = await streams.ListControlAsync(HttpContext, cancellationToken).ConfigureAwait(false);
        if (control.Unauthorized)
        {
            return Challenge();
        }

        if (!control.Succeeded)
        {
            LoadError = control.Error ?? "Could not load control list from Kithara.";
            return Page();
        }

        // Slug → GUID via control list (no get-by-slug REST).
        var match = control.Items.FirstOrDefault(s =>
            string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return NotFound();
        }

        StrunaId = match.Id;
        Title = string.IsNullOrWhiteSpace(match.Title) ? match.Slug : match.Title;
        Slug = match.Slug;
        return Page();
    }
}
