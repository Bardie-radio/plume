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

    /// <summary><c>login_form</c> or <c>redirect</c> — switch on this, never on <see cref="Id"/>.</summary>
    [JsonPropertyName("ui_mode")]
    public string UiMode { get; set; } = string.Empty;

    [JsonPropertyName("login_form")]
    public List<DiscoveryFormField> LoginForm { get; set; } = [];

    /// <summary>Legacy alias for login fields (Kithara still emits <c>form_fields</c>).</summary>
    [JsonPropertyName("form_fields")]
    public List<DiscoveryFormField> FormFields { get; set; } = [];

    [JsonPropertyName("bind_form")]
    public List<DiscoveryFormField>? BindForm { get; set; }

    [JsonPropertyName("authorize_url")]
    public string? AuthorizeUrl { get; set; }

    /// <summary>Effective login fields: <c>login_form</c> preferred, else legacy <c>form_fields</c>.</summary>
    public IReadOnlyList<DiscoveryFormField> EffectiveLoginFields =>
        LoginForm.Count > 0 ? LoginForm : FormFields;
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

    [JsonPropertyName("must_rotate_credentials")]
    public bool MustRotateCredentials { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class BindingUpdateRequestBody
{
    [JsonPropertyName("payload")]
    public Dictionary<string, string>? Payload { get; set; }

    [JsonPropertyName("ceremony")]
    public string? Ceremony { get; set; }
}

public sealed class BindingUpdateResponseBody
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("external_subject")]
    public string? ExternalSubject { get; set; }

    [JsonPropertyName("must_rotate_credentials")]
    public bool MustRotateCredentials { get; set; }

    [JsonPropertyName("must_complete_binding")]
    public bool MustCompleteBinding { get; set; }

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

    [JsonPropertyName("must_rotate_credentials")]
    public bool MustRotateCredentials { get; set; }

    [JsonPropertyName("must_complete_binding")]
    public bool MustCompleteBinding { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class RegisterRequestBody
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}

public sealed class RegisterResponseBody
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("registration_password")]
    public string? RegistrationPassword { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class ClaimRequestBody
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("registration_password")]
    public string RegistrationPassword { get; set; } = string.Empty;
}

public sealed class ClaimResponseBody
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("provider_id")]
    public string? ProviderId { get; set; }

    [JsonPropertyName("must_complete_binding")]
    public bool MustCompleteBinding { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class BffRegisterRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}

public sealed class BffRegisterResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("user_id")]
    public Guid? UserId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("registration_password")]
    public string? RegistrationPassword { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class BffClaimRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("registration_password")]
    public string RegistrationPassword { get; set; } = string.Empty;
}

public sealed class BffGuestExchangeRequest
{
    [JsonPropertyName("guest_code")]
    public string? GuestCode { get; set; }

    /// <summary>Alias for <see cref="GuestCode"/>.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
