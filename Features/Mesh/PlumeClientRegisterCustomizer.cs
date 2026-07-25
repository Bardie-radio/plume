using Bardie.Module.Channel.Manifest;
using Bardie.Module.Channel.Participant;
using Bardie.Modules.V1;

namespace Plume.Features.Mesh;

/// <summary>
/// Fills Register <c>details.client</c> — Channel never interprets kind bags.
/// Plume is user-aware: day-to-day control is REST with the end-user JWT (via BFF).
/// </summary>
public sealed class PlumeClientRegisterCustomizer : IModuleRegisterRequestCustomizer
{
    public const string AuthMode = "user-aware";

    public void Customize(RegisterRequest request, ModuleManifest manifest)
    {
        request.Client = new ClientRegisterDetails { AuthMode = AuthMode };
    }
}
