using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Pages.Account;

[Authorize(Roles = "admin")]
public class InviteModel(IKitharaAuthClient auth) : PageModel
{
    private const string TempInvitedUsername = "InvitedUsername";
    private const string TempRegistrationPassword = "InvitedRegistrationPassword";

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    public string? CreateError { get; private set; }

    public InvitedSecrets? JustInvited { get; private set; }

    public IActionResult OnGet()
    {
        ReadInvitedSecrets();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var username = Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            CreateError = "Username is required.";
            return Page();
        }

        var result = await auth
            .RegisterInviteAsync(HttpContext, username, cancellationToken)
            .ConfigureAwait(false);

        if (result.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (!result.Succeeded || result.Created is null)
        {
            CreateError = result.Error ?? "Could not create invite.";
            Response.StatusCode = result.StatusCode is HttpStatusCode.Forbidden
                or HttpStatusCode.Conflict
                or HttpStatusCode.BadRequest
                ? (int)result.StatusCode
                : StatusCodes.Status400BadRequest;
            return Page();
        }

        TempData[TempInvitedUsername] = result.Created.Username ?? username;
        TempData[TempRegistrationPassword] = result.Created.RegistrationPassword;
        return RedirectToPage();
    }

    private void ReadInvitedSecrets()
    {
        var username = TempData[TempInvitedUsername] as string;
        var password = TempData[TempRegistrationPassword] as string;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        JustInvited = new InvitedSecrets(username, password);
    }

    public sealed record InvitedSecrets(string Username, string RegistrationPassword);
}
