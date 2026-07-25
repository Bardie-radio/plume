using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Claim;

/// <summary>Public invite claim: username + one-time registration password → claim session.</summary>
[AllowAnonymous]
public class ClaimModel(
    IKitharaAuthClient auth,
    IPlumeSessionService sessions) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string RegistrationPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Claim/Bind");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Claim/Bind");
        }

        var username = Username?.Trim() ?? string.Empty;
        var password = RegistrationPassword?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Username and registration password are required.";
            return Page();
        }

        var result = await auth
            .ClaimAsync(username, password, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.Tokens is null)
        {
            ErrorMessage = result.Error ?? "Could not complete registration.";
            Response.StatusCode = result.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.BadRequest
                or HttpStatusCode.TooManyRequests
                ? (int)result.StatusCode
                : StatusCodes.Status401Unauthorized;
            return Page();
        }

        await sessions.EstablishAsync(HttpContext, result.Tokens, cancellationToken).ConfigureAwait(false);

        return RedirectToPage("/Claim/Bind");
    }
}
