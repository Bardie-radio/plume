using System.Net;
using System.Net.Http.Json;
using Plume.Features.Bff.Dtos;

namespace Plume.Features.Bff.KitharaClients;

/// <summary>Server-side Struna list/create against Kithara (Razor home; islands use <c>/bff/*</c>).</summary>
public interface IKitharaStreamsClient
{
    Task<IReadOnlyList<StrunaSummary>?> ListListenAsync(
        HttpContext http,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StrunaSummary>?> ListControlAsync(
        HttpContext http,
        CancellationToken cancellationToken = default);

    Task<CreateStrunaResult> CreateAsync(
        HttpContext http,
        CreateStrunaRequestBody body,
        CancellationToken cancellationToken = default);
}

public sealed class KitharaStreamsClient(IKitharaUpstreamClient upstream) : IKitharaStreamsClient
{
    public Task<IReadOnlyList<StrunaSummary>?> ListListenAsync(
        HttpContext http,
        CancellationToken cancellationToken = default) =>
        ListAsync(http, "streams/listen", cancellationToken);

    public Task<IReadOnlyList<StrunaSummary>?> ListControlAsync(
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

    private async Task<IReadOnlyList<StrunaSummary>?> ListAsync(
        HttpContext http,
        string apiPath,
        CancellationToken cancellationToken)
    {
        using var response = await upstream
            .SendAsync(http, HttpMethod.Get, apiPath, content: null, cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var payload = await KitharaHttp
            .TryReadJsonAsync<StrunaListResponse>(response.Content, cancellationToken)
            .ConfigureAwait(false);
        return payload?.Strunas ?? [];
    }
}
