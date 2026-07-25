using System.Net;
using System.Net.Http.Json;
using Plume.Features.Bff.Dtos;

namespace Plume.Features.Bff.KitharaClients;

/// <summary>Server-side Struna list/create against Kithara (Razor home; islands use <c>/bff/*</c>).</summary>
public interface IKitharaStreamsClient
{
    Task<StrunaListResult> ListListenAsync(
        HttpContext http,
        CancellationToken cancellationToken = default);

    Task<StrunaListResult> ListControlAsync(
        HttpContext http,
        CancellationToken cancellationToken = default);

    Task<CreateStrunaResult> CreateAsync(
        HttpContext http,
        CreateStrunaRequestBody body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unauthenticated open-playback lookup (public | hidden). <see cref="OpenStrunaResult.Struna"/>
    /// is null on 404 / non-open modes.
    /// </summary>
    Task<OpenStrunaResult> GetOpenBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="Unauthorized"/> means the Plume session is gone (Challenge).
/// <see cref="Succeeded"/> false with a message is an upstream failure (do not treat as empty).
/// </summary>
public sealed record StrunaListResult(
    bool Succeeded,
    bool Unauthorized,
    IReadOnlyList<StrunaSummary> Items,
    string? Error,
    HttpStatusCode? StatusCode);

public sealed record OpenStrunaResult(
    bool Succeeded,
    StrunaSummary? Struna,
    string? Error,
    HttpStatusCode? StatusCode);

public sealed class KitharaStreamsClient(IKitharaUpstreamClient upstream) : IKitharaStreamsClient
{
    public Task<StrunaListResult> ListListenAsync(
        HttpContext http,
        CancellationToken cancellationToken = default) =>
        ListAsync(http, "streams/listen", cancellationToken);

    public Task<StrunaListResult> ListControlAsync(
        HttpContext http,
        CancellationToken cancellationToken = default) =>
        ListAsync(http, "streams/control", cancellationToken);

    public async Task<CreateStrunaResult> CreateAsync(
        HttpContext http,
        CreateStrunaRequestBody body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(body);

        using var content = JsonContent.Create(body);
        using var response = await upstream
            .SendAsync(http, HttpMethod.Post, "streams", content, cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            return new CreateStrunaResult(
                false,
                null,
                "Session expired. Sign in again.",
                HttpStatusCode.Unauthorized);
        }

        var payload = await KitharaHttp
            .TryReadJsonAsync<CreateStrunaResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode && payload is not null && payload.Id != Guid.Empty)
        {
            return new CreateStrunaResult(true, payload, null, response.StatusCode);
        }

        var error = string.IsNullOrWhiteSpace(payload?.Error)
            ? response.StatusCode switch
            {
                HttpStatusCode.Conflict => "That slug is already in use.",
                HttpStatusCode.BadRequest => "Check the slug and access modes.",
                HttpStatusCode.Forbidden => "You cannot create a Struna.",
                _ => "Could not create Struna.",
            }
            : payload.Error;

        return new CreateStrunaResult(false, null, error, response.StatusCode);
    }

    public async Task<OpenStrunaResult> GetOpenBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var path = "streams/by-slug/" + Uri.EscapeDataString(slug.Trim());
        using var response = await upstream
            .SendUnauthenticatedAsync(HttpMethod.Get, path, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new OpenStrunaResult(false, null, null, response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new OpenStrunaResult(
                false,
                null,
                $"Kithara returned {(int)response.StatusCode} for /api/{path}.",
                response.StatusCode);
        }

        var payload = await KitharaHttp
            .TryReadJsonAsync<StrunaSummary>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || payload.Id == Guid.Empty)
        {
            return new OpenStrunaResult(false, null, "Invalid open-playback payload.", response.StatusCode);
        }

        return new OpenStrunaResult(true, payload, null, response.StatusCode);
    }

    private async Task<StrunaListResult> ListAsync(
        HttpContext http,
        string apiPath,
        CancellationToken cancellationToken)
    {
        using var response = await upstream
            .SendAsync(http, HttpMethod.Get, apiPath, content: null, cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            return new StrunaListResult(false, Unauthorized: true, [], null, HttpStatusCode.Unauthorized);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new StrunaListResult(
                false,
                Unauthorized: false,
                [],
                $"Kithara returned {(int)response.StatusCode} for /api/{apiPath}.",
                response.StatusCode);
        }

        var payload = await KitharaHttp
            .TryReadJsonAsync<StrunaListResponse>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        return new StrunaListResult(
            true,
            Unauthorized: false,
            payload?.Strunas ?? [],
            null,
            response.StatusCode);
    }
}
