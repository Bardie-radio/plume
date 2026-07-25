using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Plume.Features.Bff.Dtos;

namespace Plume.Features.Bff.KitharaClients;

/// <summary>Server-side calls to Kithara auth REST. Tokens never leave this layer toward the browser.</summary>
public interface IKitharaAuthClient
{
    Task<DiscoveryResponse?> GetDiscoveryAsync(CancellationToken cancellationToken = default);

    Task<AuthLoginResult> AuthenticateAsync(
        string providerId,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default);
}

public sealed record AuthLoginResult(
    bool Succeeded,
    SessionTokens? Tokens,
    string? Error,
    HttpStatusCode StatusCode);

public sealed class KitharaAuthClient(
    IHttpClientFactory httpClientFactory,
    IOptions<KitharaOptions> kitharaOptions) : IKitharaAuthClient
{
    public async Task<DiscoveryResponse?> GetDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = KitharaHttp.ResolveBaseUrl(kitharaOptions);
        if (baseUrl is null)
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(KitharaHttp.HttpClientName);
        using var response = await client
            .GetAsync($"{baseUrl}/api/auth/discovery", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await KitharaHttp
            .TryReadJsonAsync<DiscoveryResponse>(response.Content, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AuthLoginResult> AuthenticateAsync(
        string providerId,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(payload);

        var baseUrl = KitharaHttp.ResolveBaseUrl(kitharaOptions);
        if (baseUrl is null)
        {
            return new AuthLoginResult(
                false,
                null,
                "Kithara is not configured.",
                HttpStatusCode.BadGateway);
        }

        var client = httpClientFactory.CreateClient(KitharaHttp.HttpClientName);
        using var response = await client
            .PostAsJsonAsync(
                $"{baseUrl}/api/auth/authenticate",
                new AuthenticateRequestBody
                {
                    ProviderId = providerId,
                    Payload = payload.ToDictionary(static kv => kv.Key, static kv => kv.Value),
                },
                cancellationToken)
            .ConfigureAwait(false);

        // Non-JSON error bodies still become a safe browser-facing message below.
        var body = await KitharaHttp
            .TryReadJsonAsync<AuthenticateResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = string.IsNullOrWhiteSpace(body?.Error)
                ? "Authentication failed."
                : body.Error;
            return new AuthLoginResult(false, null, error, response.StatusCode);
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.AccessToken)
            || string.IsNullOrWhiteSpace(body.RefreshToken))
        {
            return new AuthLoginResult(
                false,
                null,
                "Authentication failed.",
                HttpStatusCode.BadGateway);
        }

        // Strip tokens into the session store only — never echo this DTO to the browser.
        return new AuthLoginResult(
            true,
            new SessionTokens(body.AccessToken, body.RefreshToken, providerId),
            null,
            HttpStatusCode.OK);
    }
}
