namespace Plume.Features.Bff;

public sealed class KitharaOptions
{
    public const string SectionName = "Kithara";

    /// <summary>Base URL of Kithara (no trailing path). BFF calls <c>{BaseUrl}/api/…</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
