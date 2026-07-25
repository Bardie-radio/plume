using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Plume.Features.Bff;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Player;

/// <summary>
/// Listen surface. Anonymous for public/hidden Strunas; private/protected still need a session.
/// Control desk stays on <c>/control/{slug}</c> with <see cref="AuthorizeAttribute"/>.
/// </summary>
[AllowAnonymous]
public class ListenModel(
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

    /// <summary>True when now-playing may poll the unauthenticated by-slug BFF path.</summary>
    public bool OpenPlayback { get; private set; }

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var slug = Slug?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        Slug = slug;

        // Prefer open by-slug (public | hidden) so anonymous + hidden URL shares work without listen list.
        var open = await streams.GetOpenBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
        if (open.Succeeded && open.Struna is not null)
        {
            ApplyMatch(open.Struna, openPlayback: true);
            await TrySetCanControlAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        if (open.StatusCode is System.Net.HttpStatusCode.BadGateway)
        {
            LoadError = open.Error ?? "Could not reach Kithara.";
            return Page();
        }

        // Private / protected (or unknown slug): need a session + listen/control lists.
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

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

        // Slug → GUID via listen/control lists (no get-by-slug REST for locked playback).
        var listenMatch = listen.Items.FirstOrDefault(s =>
            string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
        var controlMatch = control.Items.FirstOrDefault(s =>
            string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
        var match = listenMatch ?? controlMatch;

        if (match is null)
        {
            return NotFound();
        }

        ApplyMatch(match, openPlayback: IsOpenPlaybackWire(match.PlaybackAccess));
        CanControl = controlMatch is not null;
        return Page();
    }

    private async Task TrySetCanControlAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            CanControl = false;
            return;
        }

        var control = await streams.ListControlAsync(HttpContext, cancellationToken).ConfigureAwait(false);
        if (!control.Succeeded || control.Unauthorized)
        {
            CanControl = false;
            return;
        }

        CanControl = control.Items.Any(s =>
            string.Equals(s.Slug, Slug, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyMatch(StrunaSummary match, bool openPlayback)
    {
        StrunaId = match.Id;
        Title = string.IsNullOrWhiteSpace(match.Title) ? match.Slug : match.Title;
        Slug = match.Slug;
        PlaybackAccess = string.IsNullOrWhiteSpace(match.PlaybackAccess)
            ? "public"
            : match.PlaybackAccess.Trim().ToLowerInvariant();
        OpenPlayback = openPlayback;
        StreamUrl = KitharaHttp.BuildStreamUrl(kitharaOptions.Value, Slug);
    }

    private static bool IsOpenPlaybackWire(string? value) =>
        value?.Trim().ToLowerInvariant() is "public" or "hidden";
}
