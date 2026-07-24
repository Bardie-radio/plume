namespace Plume.Features.Bff;

/// <summary>
/// Plume BFF session boundary. Phase 3 login calls <see cref="EstablishAsync"/> after Kithara authenticate.
/// </summary>
public interface IPlumeSessionService
{
    /// <summary>Issues a new session id (anti-fixation), stores tokens, sets the httpOnly cookie.</summary>
    Task EstablishAsync(HttpContext http, SessionTokens tokens, CancellationToken cancellationToken = default);

    Task ClearAsync(HttpContext http, CancellationToken cancellationToken = default);

    Task<SessionTokens?> TryGetAsync(HttpContext http, CancellationToken cancellationToken = default);

    /// <summary>Replaces stored tokens for the current session cookie without rotating the id.</summary>
    bool TryUpdateTokens(HttpContext http, SessionTokens tokens);
}
