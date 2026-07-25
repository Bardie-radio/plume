using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages;

[Authorize]
public class IndexModel(IKitharaStreamsClient streams) : PageModel
{
    private const string TempGuestCode = "CreatedGuestCode";
    private const string TempListenToken = "CreatedListenToken";
    private const string TempCreatedSlug = "CreatedSlug";
    private const string TempCreatedTitle = "CreatedTitle";
    private const string TempCreatedId = "CreatedId";

    [BindProperty]
    public string Slug { get; set; } = string.Empty;

    [BindProperty]
    public string? Title { get; set; }

    // PLUME-CONTRACT-001 — hand-copied Kithara wire defaults; replace with shared contracts.
    [BindProperty]
    public string PlaybackAccess { get; set; } = "public";

    [BindProperty]
    public string ControlAccess { get; set; } = "private";

    public IReadOnlyList<HomeStrunaRow> Strunas { get; private set; } = [];

    public string? LoadError { get; private set; }

    public string? CreateError { get; private set; }

    /// <summary>One-shot secrets from the last create (TempData — gone after this render).</summary>
    public CreatedSecrets? JustCreated { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ReadCreatedSecrets();
        return await LoadListsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var slug = Slug?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slug))
        {
            CreateError = "Slug is required.";
            return await LoadListsAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await streams
            .CreateAsync(
                HttpContext,
                new CreateStrunaRequestBody
                {
                    Slug = slug,
                    Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
                    PlaybackAccess = NormalizePlayback(PlaybackAccess),
                    ControlAccess = NormalizeControl(ControlAccess),
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (result.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (!result.Succeeded || result.Created is null)
        {
            CreateError = result.Error ?? "Could not create Struna.";
            Response.StatusCode = result.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest
                ? (int)result.StatusCode
                : StatusCodes.Status400BadRequest;
            return await LoadListsAsync(cancellationToken).ConfigureAwait(false);
        }

        // Guest code / listen token: owner reads them on the control desk (spoiler).
        // Still pass via TempData so the create banner can link / mention listen token once.
        TempData[TempCreatedSlug] = result.Created.Slug;
        TempData[TempCreatedTitle] = result.Created.Title;
        TempData[TempCreatedId] = result.Created.Id.ToString("D");
        if (!string.IsNullOrWhiteSpace(result.Created.GuestCode))
        {
            TempData[TempGuestCode] = result.Created.GuestCode;
        }

        if (!string.IsNullOrWhiteSpace(result.Created.ListenToken))
        {
            TempData[TempListenToken] = result.Created.ListenToken;
        }

        return RedirectToPage();
    }

    private async Task<IActionResult> LoadListsAsync(CancellationToken cancellationToken)
    {
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
            Strunas = [];
            return Page();
        }

        var byId = new Dictionary<Guid, HomeStrunaRow>();

        foreach (var s in listen.Items)
        {
            byId[s.Id] = ToRow(s, canListen: true, canControl: false);
        }

        foreach (var s in control.Items)
        {
            if (byId.TryGetValue(s.Id, out var existing))
            {
                byId[s.Id] = existing with { CanControl = true };
            }
            else
            {
                byId[s.Id] = ToRow(s, canListen: false, canControl: true);
            }
        }

        Strunas = byId.Values
            .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Page();
    }

    private void ReadCreatedSecrets()
    {
        var slug = TempData[TempCreatedSlug] as string;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        Guid? id = null;
        if (TempData[TempCreatedId] is string idText && Guid.TryParse(idText, out var parsed))
        {
            id = parsed;
        }

        JustCreated = new CreatedSecrets(
            id,
            slug,
            TempData[TempCreatedTitle] as string ?? slug,
            TempData[TempGuestCode] as string,
            TempData[TempListenToken] as string);
    }

    private static HomeStrunaRow ToRow(StrunaSummary s, bool canListen, bool canControl) =>
        new(s.Id, s.Slug, string.IsNullOrWhiteSpace(s.Title) ? s.Slug : s.Title, s.PlaybackAccess, s.ControlAccess, canListen, canControl);

    // PLUME-CONTRACT-001 — mirrors Kithara ParsePlayback / ParseControl; do not add modes here.
    private static string NormalizePlayback(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "protected" => "protected",
            "private" => "private",
            "hidden" => "hidden",
            _ => "public",
        };

    private static string NormalizeControl(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "protected" => "protected",
            _ => "private",
        };

    public sealed record HomeStrunaRow(
        Guid Id,
        string Slug,
        string Title,
        string PlaybackAccess,
        string ControlAccess,
        bool CanListen,
        bool CanControl);

    public sealed record CreatedSecrets(
        Guid? Id,
        string Slug,
        string Title,
        string? GuestCode,
        string? ListenToken);
}
