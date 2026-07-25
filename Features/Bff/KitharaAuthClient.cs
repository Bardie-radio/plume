using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

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
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(BffEndpoints.HttpClientName);
        using var response = await client
            .GetAsync($"{baseUrl}/api/auth/discovery", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            return await response.Content
                .ReadFromJsonAsync<DiscoveryResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<AuthLoginResult> AuthenticateAsync(
        string providerId,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(payload);

        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
        {
            return new AuthLoginResult(
                false,
                null,
                "Kithara is not configured.",
                HttpStatusCode.BadGateway);
        }

        var client = httpClientFactory.CreateClient(BffEndpoints.HttpClientName);
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

        AuthenticateResponseBody? body = null;
        try
        {
            body = await response.Content
                .ReadFromJsonAsync<AuthenticateResponseBody>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Non-JSON error bodies still become a safe browser-facing message below.
        }

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

    private string? GetBaseUrl()
    {
        var baseUrl = kitharaOptions.Value.BaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
    }
}
