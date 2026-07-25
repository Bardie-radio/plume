using System.Net;
using Microsoft.AspNetCore.Mvc;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Features.Bff;

/// <summary>
/// Session-establish routes under <c>/bff</c>. Authenticate / refresh stay server-internal
/// (login client + proxy refresh). The catch-all proxy does not serve <c>/auth/*</c>.
/// </summary>
public static class BffAuthEndpoints
{
    public static RouteGroupBuilder MapBffAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/discovery", DiscoveryAsync);
        group.MapPost("/auth/login", LoginAsync);
        group.MapPost("/auth/logout", LogoutAsync);

        // Claim this path so the catch-all never mirrors guest JWTs to the browser.
        // feat-guest replaces the stub with exchange → EstablishAsync.
        group.MapPost("/streams/{strunaId}/guest/exchange", GuestExchangeNotImplemented);

        return group;
    }

    private static IResult GuestExchangeNotImplemented() =>
        Results.Json(
            new { error = "Guest exchange is not available yet." },
            statusCode: StatusCodes.Status501NotImplemented);

    private static async Task<IResult> DiscoveryAsync(
        IKitharaAuthClient auth,
        CancellationToken cancellationToken)
    {
        var discovery = await auth.GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        if (discovery is null)
        {
            return Results.Json(
                new { error = "Auth discovery is unavailable." },
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Json(discovery);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] BffLoginRequest? body,
        IKitharaAuthClient auth,
        IPlumeSessionService sessions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ProviderId))
        {
            return Results.Json(
                new BffLoginResponse { Ok = false, Error = "provider_id is required." },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var payload = body.Payload ?? new Dictionary<string, string>();
        var result = await auth
            .AuthenticateAsync(body.ProviderId, payload, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.Tokens is null)
        {
            var status = result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? (int)result.StatusCode
                : StatusCodes.Status401Unauthorized;

            return Results.Json(
                new BffLoginResponse
                {
                    Ok = false,
                    Error = result.Error ?? "Authentication failed.",
                },
                statusCode: status);
        }

        await sessions.EstablishAsync(http, result.Tokens, cancellationToken).ConfigureAwait(false);

        // Success/error only — never access_token / refresh_token.
        return Results.Json(new BffLoginResponse { Ok = true });
    }

    private static async Task<IResult> LogoutAsync(
        IPlumeSessionService sessions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        await sessions.ClearAsync(http, cancellationToken).ConfigureAwait(false);

        // Form posts from layout follow the redirect; fetch clients can ignore Location.
        return Results.Redirect("/login");
    }
}
