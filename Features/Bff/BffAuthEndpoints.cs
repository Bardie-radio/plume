using System.Net;
using Microsoft.AspNetCore.Mvc;
using Plume.Features.Bff.Dtos;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Features.Bff;

/// <summary>
/// Session-establish routes under <c>/bff</c>. Authenticate / refresh stay server-internal
/// (login client + proxy refresh). The catch-all proxy does not serve <c>/auth/*</c>.
/// Guest exchange is claimed here so JWTs never mirror through the catch-all.
/// </summary>
public static class BffAuthEndpoints
{
    public static RouteGroupBuilder MapBffAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/discovery", DiscoveryAsync);
        group.MapPost("/auth/login", LoginAsync);
        group.MapPost("/auth/logout", LogoutAsync);

        group.MapPost("/streams/{strunaId:guid}/guest/exchange", GuestExchangeAsync);
        group.MapPost("/streams/by-slug/{slug}/guest/exchange", GuestExchangeBySlugAsync);

        return group;
    }

    private static async Task<IResult> GuestExchangeAsync(
        Guid strunaId,
        [FromBody] BffGuestExchangeRequest? body,
        IKitharaGuestClient guests,
        IPlumeSessionService sessions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var code = body?.GuestCode ?? body?.Code;
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.Json(
                new BffLoginResponse { Ok = false, Error = "guest_code is required." },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await guests
            .ExchangeAsync(strunaId, code, cancellationToken)
            .ConfigureAwait(false);

        return await FinishGuestExchangeAsync(result, sessions, http, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GuestExchangeBySlugAsync(
        string slug,
        [FromBody] BffGuestExchangeRequest? body,
        IKitharaGuestClient guests,
        IPlumeSessionService sessions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Results.Json(
                new BffLoginResponse { Ok = false, Error = "slug is required." },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var code = body?.GuestCode ?? body?.Code;
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.Json(
                new BffLoginResponse { Ok = false, Error = "guest_code is required." },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await guests
            .ExchangeBySlugAsync(slug, code, cancellationToken)
            .ConfigureAwait(false);

        return await FinishGuestExchangeAsync(result, sessions, http, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> FinishGuestExchangeAsync(
        AuthLoginResult result,
        IPlumeSessionService sessions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded || result.Tokens is null)
        {
            var status = result.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.NotFound
                or HttpStatusCode.TooManyRequests
                ? (int)result.StatusCode
                : StatusCodes.Status401Unauthorized;

            return Results.Json(
                new BffLoginResponse
                {
                    Ok = false,
                    Error = result.Error ?? "Guest exchange failed.",
                },
                statusCode: status);
        }

        await sessions.EstablishAsync(http, result.Tokens, cancellationToken).ConfigureAwait(false);

        // Success/error only — never access_token / refresh_token.
        return Results.Json(new BffLoginResponse { Ok = true });
    }

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
