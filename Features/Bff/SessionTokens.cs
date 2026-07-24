namespace Plume.Features.Bff;

/// <summary>Server-side Kithara credentials bound to a Plume session. Never sent to the browser.</summary>
public sealed record SessionTokens(
    string AccessToken,
    string RefreshToken,
    string ProviderId);
