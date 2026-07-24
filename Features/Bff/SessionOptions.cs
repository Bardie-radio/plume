namespace Plume.Features.Bff;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    public string CookieName { get; set; } = "plume.sid";

    /// <summary>SameSite mode name: <c>Lax</c>, <c>Strict</c>, or <c>None</c>.</summary>
    public string SameSite { get; set; } = "Lax";

    /// <summary>
    /// Sliding idle lifetime for in-memory session entries. Abandoned sessions are dropped on
    /// next access after this window. Default 24 hours; <see cref="TimeSpan.Zero"/> or negative disables expiry.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(24);

    public SameSiteMode SameSiteMode =>
        Enum.TryParse<SameSiteMode>(SameSite, ignoreCase: true, out var mode)
            ? mode
            : SameSiteMode.Lax;
}
