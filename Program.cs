using Bardie.Logos.Channel.Participant;
using Bardie.Logos.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Plume.Features.Bff;
using Plume.Features.Hosting;
using Plume.Features.Mesh;

var builder = WebApplication.CreateBuilder(args);

var manifest = builder.AddBardieModuleHosting(
    configure: options =>
    {
        options.ServerDnsNames = ["plume", "localhost"];
        options.ExpectedHostClientIdentity = "kithara";
    },
    otelFallbackServiceName: "bardie.plume");

builder.Services.AddSingleton<IModuleRegisterRequestCustomizer, PlumeClientRegisterCustomizer>();

builder.Services.AddRazorPages();
builder.Services.AddPlumeBff(builder.Configuration);

// PLUME-FWD-001: honor X-Forwarded-Proto only when BARDIE_FORWARDED_HEADERS_* is set.
// Unset = no proxy (Secure cookies follow the direct connection).
if (ForwardedHeadersConfiguration.IsEnabled(builder.Configuration))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        ForwardedHeadersConfiguration.Apply(options, builder.Configuration));
}

// Persist antiforgery keys when configured (Compose: /app/dp-keys volume).
// Ephemeral in-memory keys are fine for bare `dotnet run`.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Environment.GetEnvironmentVariable("BARDIE_DP_KEYS_PATH");
if (!string.IsNullOrWhiteSpace(dpKeysPath))
{
    Directory.CreateDirectory(dpKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));
}

var app = builder.Build();

await app.EnsureModuleParticipantServerCertificateAsync().ConfigureAwait(false);

var participantOptions = app.Services.GetRequiredService<IOptions<ModuleParticipantOptions>>().Value;
var httpPort = ModuleHostingPorts.ResolveHttpPort(builder.Configuration);
app.Logger.LogInformation(
    """

    ======================================================================
      PLUME starting — {Slug} ({Otel})
    ----------------------------------------------------------------------
      HTTP :{HttpPort}  ·  work gRPC :{Port} (idle)
      host={Host}  ·  register={Register}
    ======================================================================
    """,
    manifest.Slug,
    manifest.OtelServiceName,
    httpPort,
    participantOptions.WorkGrpcPort,
    participantOptions.HostGrpcAddress,
    participantOptions.EnableRegistration);

if (ForwardedHeadersConfiguration.IsEnabled(app.Configuration))
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // HSTS belongs on the TLS-terminating edge (Traefik / nginx), not on the
    // internal HTTP Plume container — emitting it here breaks HTTP-only edges.
}

// Local Compose / bundled edge is HTTP-only; skip redirect noise when no HTTPS port is configured.
var httpsPort = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")
    ?? builder.Configuration["HTTPS_PORTS"];
if (!string.IsNullOrWhiteSpace(httpsPort)
    || builder.Configuration.GetValue<bool>("UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

// Vite emits wwwroot/dist — serve before auth so CSS/JS are never gated.
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<ContentSecurityPolicyMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ClaimBindRedirectMiddleware>();
app.UseAuthorization();
app.UseMiddleware<BffAntiforgeryMiddleware>();

// healthz only — MapModuleHostingEndpoints also maps `/`, which would steal the Razor home.
app.MapGet("/healthz", () => Results.Ok(new { ok = true, slug = manifest.Slug }));

app.MapStaticAssets();
app.MapBffEndpoints();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
