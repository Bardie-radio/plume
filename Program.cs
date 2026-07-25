using Bardie.Module.Channel.Participant;
using Bardie.Module.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Plume.Features.Bff;
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
    "Plume starting as {Slug} ({Otel}); HTTP :{HttpPort}; work gRPC :{Port} (idle); host={Host}; register={Register}",
    manifest.Slug,
    manifest.OtelServiceName,
    httpPort,
    participantOptions.WorkGrpcPort,
    participantOptions.HostGrpcAddress,
    participantOptions.EnableRegistration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Local Compose is HTTP-only; skip redirect noise when no HTTPS port is configured.
var httpsPort = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")
    ?? builder.Configuration["HTTPS_PORTS"];
if (!string.IsNullOrWhiteSpace(httpsPort)
    || builder.Configuration.GetValue<bool>("UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// healthz only — MapModuleHostingEndpoints also maps `/`, which would steal the Razor home.
app.MapGet("/healthz", () => Results.Ok(new { ok = true, slug = manifest.Slug }));

app.MapStaticAssets();
app.MapBffEndpoints();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
