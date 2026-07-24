namespace Plume.Features.Bff;

public interface ISessionTokenStore
{
    void Set(string sessionId, SessionTokens tokens);

    bool TryGet(string sessionId, out SessionTokens tokens);

    void Remove(string sessionId);
}
