using Microsoft.AspNetCore.DataProtection;
using Plume.Features.Bff;

var builder = WebApplication.CreateBuilder(args);

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

app.MapStaticAssets();
app.MapBffEndpoints();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
