using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

public static class BffServiceCollectionExtensions
{
    public static IServiceCollection AddPlumeBff(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KitharaOptions>(configuration.GetSection(KitharaOptions.SectionName));
        services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));

        services.AddSingleton<ISessionTokenStore, MemorySessionTokenStore>();
        services.AddSingleton<IPlumeSessionService, PlumeSessionService>();

        services.AddHttpClient(BffEndpoints.HttpClientName, (sp, client) =>
        {
            var baseUrl = sp.GetRequiredService<IOptions<KitharaOptions>>().Value.BaseUrl;
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var normalized = baseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(normalized);
            }
        });

        return services;
    }
}
