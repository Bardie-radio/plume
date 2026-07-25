using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Plume.Features.Bff;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Player;

[Authorize]
public class IndexModel(
    IKitharaStreamsClient streams,
    IOptions<KitharaOptions> kitharaOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    public Guid StrunaId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string PlaybackAccess { get; private set; } = "public";

    /// <summary>Browser stream URL without listen token (absolute or relative).</summary>
    public string StreamUrl { get; private set; } = string.Empty;

    public bool CanControl { get; private set; }

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var slug = Slug?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        Slug = slug;

        var listenTask = streams.ListListenAsync(HttpContext, cancellationToken);
        var controlTask = streams.ListControlAsync(HttpContext, cancellationToken);
        await Task.WhenAll(listenTask, controlTask).ConfigureAwait(false);

        var listen = await listenTask.ConfigureAwait(false);
        var control = await controlTask.ConfigureAwait(false);

        if (listen.Unauthorized || control.Unauthorized)
        {
            return Challenge();
        }

        if (!listen.Succeeded || !control.Succeeded)
        {
            LoadError = listen.Error ?? control.Error ?? "Could not load Strunas from Kithara.";
            return Page();
        }

        // Slug → GUID via listen/control lists (no get-by-slug REST).
        var listenMatch = listen.Items.FirstOrDefault(s =>
            string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
        var controlMatch = control.Items.FirstOrDefault(s =>
            string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
        var match = listenMatch ?? controlMatch;

        if (match is null)
        {
            return NotFound();
        }

        StrunaId = match.Id;
        Title = string.IsNullOrWhiteSpace(match.Title) ? match.Slug : match.Title;
        Slug = match.Slug;
        PlaybackAccess = string.IsNullOrWhiteSpace(match.PlaybackAccess)
            ? "public"
            : match.PlaybackAccess.Trim().ToLowerInvariant();
        CanControl = controlMatch is not null;
        StreamUrl = KitharaHttp.BuildStreamUrl(kitharaOptions.Value, Slug);
        return Page();
    }
}
