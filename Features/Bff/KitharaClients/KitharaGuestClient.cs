using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Plume.Features.Bff.Dtos;

namespace Plume.Features.Bff.KitharaClients;

/// <summary>Unauthenticated guest code exchange against Kithara (tokens stay server-side).</summary>
public interface IKitharaGuestClient
{
    Task<AuthLoginResult> ExchangeAsync(
        Guid strunaId,
        string guestCode,
        CancellationToken cancellationToken = default);

    Task<AuthLoginResult> ExchangeBySlugAsync(
        string slug,
        string guestCode,
        CancellationToken cancellationToken = default);
}

public sealed class KitharaGuestClient(
    IHttpClientFactory httpClientFactory,
    IOptions<KitharaOptions> kitharaOptions) : IKitharaGuestClient
{
    public const string ProviderId = "kithara.guest";

    public Task<AuthLoginResult> ExchangeAsync(
        Guid strunaId,
        string guestCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestCode);
        return ExchangeAtAsync(
            $"api/streams/{strunaId:D}/guest/exchange",
            guestCode,
            cancellationToken);
    }

    public Task<AuthLoginResult> ExchangeBySlugAsync(
        string slug,
        string guestCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestCode);
        var normalized = slug.Trim().ToLowerInvariant();
        return ExchangeAtAsync(
            $"api/streams/by-slug/{Uri.EscapeDataString(normalized)}/guest/exchange",
            guestCode,
            cancellationToken);
    }

    private async Task<AuthLoginResult> ExchangeAtAsync(
        string relativePath,
        string guestCode,
        CancellationToken cancellationToken)
    {
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
                $"{baseUrl}/{relativePath.TrimStart('/')}",
                new GuestExchangeRequestBody { GuestCode = guestCode.Trim() },
                cancellationToken)
            .ConfigureAwait(false);

        var body = await KitharaHttp
            .TryReadJsonAsync<AuthenticateResponseBody>(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = string.IsNullOrWhiteSpace(body?.Error)
                ? response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => "Too many attempts. Try again later.",
                    HttpStatusCode.NotFound => "Unknown Struna.",
                    HttpStatusCode.Unauthorized => "Invalid guest code.",
                    HttpStatusCode.BadRequest => "Guest code is required.",
                    _ => "Guest exchange failed.",
                }
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
                "Guest exchange failed.",
                HttpStatusCode.BadGateway);
        }

        return new AuthLoginResult(
            true,
            new SessionTokens(body.AccessToken, body.RefreshToken, ProviderId),
            null,
            HttpStatusCode.OK);
    }

    private sealed class GuestExchangeRequestBody
    {
        [JsonPropertyName("guest_code")]
        public string GuestCode { get; set; } = string.Empty;
    }
}
