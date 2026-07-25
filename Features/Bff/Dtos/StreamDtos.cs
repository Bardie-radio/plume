using System.Text.Json.Serialization;

namespace Plume.Features.Bff.Dtos;

/// <summary>Wire DTOs for Kithara Struna list/create (<c>/api/streams</c>).</summary>
public sealed class StrunaListResponse
{
    [JsonPropertyName("strunas")]
    public List<StrunaSummary> Strunas { get; set; } = [];
}

public sealed class StrunaSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("playback_access")]
    public string PlaybackAccess { get; set; } = string.Empty;

    [JsonPropertyName("control_access")]
    public string ControlAccess { get; set; } = string.Empty;

    [JsonPropertyName("owner_user_id")]
    public Guid OwnerUserId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CreateStrunaRequestBody
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("playback_access")]
    public string PlaybackAccess { get; set; } = "public";

    [JsonPropertyName("control_access")]
    public string ControlAccess { get; set; } = "private";
}

public sealed class CreateStrunaResponseBody
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("playback_access")]
    public string PlaybackAccess { get; set; } = string.Empty;

    [JsonPropertyName("control_access")]
    public string ControlAccess { get; set; } = string.Empty;

    [JsonPropertyName("guest_code")]
    public string? GuestCode { get; set; }

    [JsonPropertyName("listen_token")]
    public string? ListenToken { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed record CreateStrunaResult(
    bool Succeeded,
    CreateStrunaResponseBody? Created,
    string? Error,
    System.Net.HttpStatusCode StatusCode);
