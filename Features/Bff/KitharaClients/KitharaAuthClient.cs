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

    Task<AuthBindingResult> UpdateBindingAsync(
        HttpContext http,
        string providerId,
        IReadOnlyDictionary<string, string> payload,
        string? ceremony = null,
        CancellationToken cancellationToken = default);

    Task<AuthRegisterResult> RegisterInviteAsync(
        HttpContext http,
        string username,
        CancellationToken cancellationToken = default);

    Task<AuthLoginResult> ClaimAsync(
        string username,
        string registrationPassword,
        CancellationToken cancellationToken = default);
}

public static class KitharaAuthConstants
{
    public const string ClaimProviderId = "kithara.claim";
}

public sealed record AuthLoginResult(
    bool Succeeded,
    SessionTokens? Tokens,
    string? Error,
    HttpStatusCode StatusCode,
    bool MustRotateCredentials = false,
    bool MustCompleteBinding = false);

public sealed record AuthRegisterResult(
    bool Succeeded,
    RegisterResponseBody? Created,
    string? Error,
    HttpStatusCode StatusCode);

public sealed record AuthBindingResult(
    bool Succeeded,
    bool MustRotateCredentials,
    string? Error,
    HttpStatusCode StatusCode,
    bool MustCompleteBinding = false);

public sealed class KitharaAuthClient(
    IHttpClientFactory httpClientFactory,
    IKitharaUpstreamClient upstream,
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
            HttpStatusCode.OK,
            body.MustRotateCredentials);
    }

    public async Task<AuthBindingResult> UpdateBindingAsync(
        HttpContext http,
        string providerId,
        IReadOnlyDictionary<string, string> payload,
        string? ceremony = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(payload);

        using var content = JsonContent.Create(new BindingUpdateRequestBody
        {
            Payload = payload.ToDictionary(static kv => kv.Key, static kv => kv.Value),
            Ceremony = ceremony,
        });
        using var response = await upstream
            .SendAsync(
                http,
                HttpMethod.Post,
                $"auth/bindings/{Uri.EscapeDataString(providerId)}",
                content,
                cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            return new AuthBindingResult(
                false,
                false,
                "Session expired. Sign in again.",
                HttpStatusCode.Unauthorized);
        }

        var body = await KitharaHttp
            .TryReadJsonAsync<BindingUpdateResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = string.IsNullOrWhiteSpace(body?.Error)
                ? "Binding update failed."
                : body.Error;
            return new AuthBindingResult(false, false, error, response.StatusCode);
        }

        return new AuthBindingResult(
            true,
            body?.MustRotateCredentials ?? false,
            null,
            HttpStatusCode.OK,
            body?.MustCompleteBinding ?? false);
    }

    public async Task<AuthRegisterResult> RegisterInviteAsync(
        HttpContext http,
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var content = JsonContent.Create(new RegisterRequestBody { Username = username.Trim() });
        using var response = await upstream
            .SendAsync(http, HttpMethod.Post, "auth/register", content, cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            return new AuthRegisterResult(
                false,
                null,
                "Session expired. Sign in again.",
                HttpStatusCode.Unauthorized);
        }

        var body = await KitharaHttp
            .TryReadJsonAsync<RegisterResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = string.IsNullOrWhiteSpace(body?.Error)
                ? response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "Admin access is required to invite users.",
                    HttpStatusCode.Conflict => "That username is already taken.",
                    _ => "Could not create invite.",
                }
                : body.Error;
            return new AuthRegisterResult(false, null, error, response.StatusCode);
        }

        if (body is null
            || body.UserId == Guid.Empty
            || string.IsNullOrWhiteSpace(body.RegistrationPassword))
        {
            return new AuthRegisterResult(
                false,
                null,
                "Invite creation failed.",
                HttpStatusCode.BadGateway);
        }

        return new AuthRegisterResult(true, body, null, HttpStatusCode.OK);
    }

    public async Task<AuthLoginResult> ClaimAsync(
        string username,
        string registrationPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationPassword);

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
                $"{baseUrl}/api/auth/claim",
                new ClaimRequestBody
                {
                    Username = username.Trim(),
                    RegistrationPassword = registrationPassword,
                },
                cancellationToken)
            .ConfigureAwait(false);

        var body = await KitharaHttp
            .TryReadJsonAsync<ClaimResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = string.IsNullOrWhiteSpace(body?.Error)
                ? "Claim failed."
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
                "Claim failed.",
                HttpStatusCode.BadGateway);
        }

        var providerId = string.IsNullOrWhiteSpace(body.ProviderId)
            ? KitharaAuthConstants.ClaimProviderId
            : body.ProviderId;

        return new AuthLoginResult(
            true,
            new SessionTokens(body.AccessToken, body.RefreshToken, providerId),
            null,
            HttpStatusCode.OK,
            MustCompleteBinding: body.MustCompleteBinding);
    }
}
