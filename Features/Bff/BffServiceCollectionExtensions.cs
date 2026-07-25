using Microsoft.AspNetCore.Authentication;
using Plume.Features.Bff.KitharaClients;

namespace Plume.Features.Bff;

public static class BffServiceCollectionExtensions
{
    public static IServiceCollection AddPlumeBff(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KitharaOptions>(configuration.GetSection(KitharaOptions.SectionName));
        services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));
        services.Configure<RouteOptions>(options =>
            options.ConstraintMap["bffNonAuth"] = typeof(BffNonAuthProxyConstraint));

        services.AddSingleton<ISessionTokenStore, MemorySessionTokenStore>();
        services.AddSingleton<IPlumeSessionService, PlumeSessionService>();
        services.AddSingleton<IKitharaAuthClient, KitharaAuthClient>();
        services.AddSingleton<IKitharaUpstreamClient, KitharaUpstreamClient>();
        services.AddSingleton<IKitharaStreamsClient, KitharaStreamsClient>();

        services
            .AddAuthentication(PlumeSessionDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, PlumeSessionAuthenticationHandler>(
                PlumeSessionDefaults.AuthenticationScheme,
                _ => { });

        services.AddAuthorization();

        // Absolute upstream URLs are built per request; do not set BaseAddress.
        services.AddHttpClient(KitharaHttp.HttpClientName);

        return services;
    }
}
