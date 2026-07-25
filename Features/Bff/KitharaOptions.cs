namespace Plume.Features.Bff;

public sealed class KitharaOptions
{
    public const string SectionName = "Kithara";

    /// <summary>Base URL of Kithara (no trailing path). BFF calls <c>{BaseUrl}/api/…</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Browser-facing Kithara origin for <c>/stream/{slug}</c> (no trailing path).
    /// Empty → relative <c>/stream/…</c> (same-host edge). Local Compose uses
    /// <c>http://localhost:8080</c> while Plume is on another port.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
