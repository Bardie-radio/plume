namespace Plume.Features.Bff;

public static class BffServiceCollectionExtensions
{
    public static IServiceCollection AddPlumeBff(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KitharaOptions>(configuration.GetSection(KitharaOptions.SectionName));
        services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));

        services.AddSingleton<ISessionTokenStore, MemorySessionTokenStore>();
        services.AddSingleton<IPlumeSessionService, PlumeSessionService>();

        // Absolute upstream URLs are built per request; do not set BaseAddress.
        services.AddHttpClient(BffEndpoints.HttpClientName);

        return services;
    }
}
