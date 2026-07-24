using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

/// <summary>In-process token store. Fine for early MVP; not multi-replica safe.</summary>
public sealed class MemorySessionTokenStore(IOptions<SessionOptions> sessionOptions) : ISessionTokenStore
{
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private readonly TimeSpan _idleTimeout = sessionOptions.Value.IdleTimeout;
    private readonly TimeProvider _time = TimeProvider.System;

    public void Set(string sessionId, SessionTokens tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(tokens);
        _sessions[sessionId] = new Entry(tokens, _time.GetUtcNow());
    }

    public bool TryGet(string sessionId, out SessionTokens tokens)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            tokens = null!;
            return false;
        }

        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            tokens = null!;
            return false;
        }

        var now = _time.GetUtcNow();
        if (_idleTimeout > TimeSpan.Zero && now - entry.LastAccessedUtc > _idleTimeout)
        {
            // Conditional remove so a concurrent Set is not discarded.
            if (_sessions.TryRemove(KeyValuePair.Create(sessionId, entry)))
            {
                tokens = null!;
                return false;
            }

            // Entry was replaced while we decided it was stale — re-evaluate.
            return TryGet(sessionId, out tokens);
        }

        entry.LastAccessedUtc = now;
        tokens = entry.Tokens;
        return true;
    }

    public void Remove(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        _sessions.TryRemove(sessionId, out _);
    }

    private sealed class Entry(SessionTokens tokens, DateTimeOffset lastAccessedUtc)
    {
        public SessionTokens Tokens { get; } = tokens;
        public DateTimeOffset LastAccessedUtc { get; set; } = lastAccessedUtc;
    }
}
