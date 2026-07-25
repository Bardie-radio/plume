namespace Plume.Pages.Shared;

/// <summary>Razor mount props for a Vue CSR island (<c>data-plume-island</c>).</summary>
public sealed class IslandHostModel
{
    public required string Island { get; init; }

    public string StrunaId { get; init; } = "";

    public string StrunaSlug { get; init; } = "";

    /// <summary>Optional; used by now-playing (<c>compact</c> | <c>prominent</c>).</summary>
    public string? Variant { get; init; }

    /// <summary>Optional; used by audio (Kithara <c>/stream/{slug}</c> URL).</summary>
    public string? StreamUrl { get; init; }
}
