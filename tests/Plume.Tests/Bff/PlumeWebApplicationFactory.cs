using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Plume.Features.Bff;

namespace Plume.Tests.Bff;

public sealed class PlumeWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeKitharaHandler Kithara { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Kithara:BaseUrl", "http://kithara.test");
        builder.UseSetting("Session:CookieName", "plume.sid");
        builder.UseSetting("Session:SameSite", "Lax");
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Kithara);
            services.AddHttpClient(BffEndpoints.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(sp =>
                    new NonDisposingHandler(sp.GetRequiredService<FakeKitharaHandler>()))
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        });
    }

    /// <summary>
    /// HttpClientFactory disposes primary handlers; keep the shared fake alive for the factory lifetime.
    /// </summary>
    private sealed class NonDisposingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override void Dispose(bool disposing)
        {
            // Intentionally do not dispose the shared FakeKitharaHandler.
        }
    }
}
