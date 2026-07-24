using System.Collections.Concurrent;

namespace Plume.Features.Bff;

/// <summary>In-process token store. Fine for early MVP; not multi-replica safe.</summary>
public sealed class MemorySessionTokenStore : ISessionTokenStore
{
    private readonly ConcurrentDictionary<string, SessionTokens> _sessions = new(StringComparer.Ordinal);

    public void Set(string sessionId, SessionTokens tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(tokens);
        _sessions[sessionId] = tokens;
    }

    public bool TryGet(string sessionId, out SessionTokens tokens)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            tokens = null!;
            return false;
        }

        return _sessions.TryGetValue(sessionId, out tokens!);
    }

    public void Remove(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        _sessions.TryRemove(sessionId, out _);
    }
}
