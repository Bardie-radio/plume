namespace Plume.Features.Bff;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    public string CookieName { get; set; } = "plume.sid";

    /// <summary>SameSite mode name: <c>Lax</c>, <c>Strict</c>, or <c>None</c>.</summary>
    public string SameSite { get; set; } = "Lax";

    public SameSiteMode SameSiteMode =>
        Enum.TryParse<SameSiteMode>(SameSite, ignoreCase: true, out var mode)
            ? mode
            : SameSiteMode.Lax;
}
