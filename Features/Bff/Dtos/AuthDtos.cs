using System.Text.Json.Serialization;

namespace Plume.Features.Bff.Dtos;

/// <summary>Wire DTOs for Kithara discovery / authenticate (live JSON names, not proto oneof).</summary>
public sealed class DiscoveryResponse
{
    [JsonPropertyName("providers")]
    public List<DiscoveryProvider> Providers { get; set; } = [];
}

public sealed class DiscoveryProvider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("module")]
    public string Module { get; set; } = string.Empty;

    /// <summary><c>form_schema</c> or <c>redirect</c> — switch on this, never on <see cref="Id"/>.</summary>
    [JsonPropertyName("ui_mode")]
    public string UiMode { get; set; } = string.Empty;

    [JsonPropertyName("form_fields")]
    public List<DiscoveryFormField> FormFields { get; set; } = [];

    [JsonPropertyName("authorize_url")]
    public string? AuthorizeUrl { get; set; }
}

public sealed class DiscoveryFormField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("input_type")]
    public string InputType { get; set; } = "text";

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public sealed class AuthenticateRequestBody
{
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public Dictionary<string, string>? Payload { get; set; }
}

public sealed class AuthenticateResponseBody
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class BffLoginRequest
{
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public Dictionary<string, string>? Payload { get; set; }
}

public sealed class BffLoginResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class BffGuestExchangeRequest
{
    [JsonPropertyName("guest_code")]
    public string? GuestCode { get; set; }

    /// <summary>Alias for <see cref="GuestCode"/>.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
