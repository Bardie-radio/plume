# Build from the parent folder that contains both `plume/` and `kithara/`
# (multi-root / Local Compose sibling layout → ProjectReference):
#
#   docker build -f plume/Dockerfile -t plume .
#
# Standalone Plume-only builds need published Bardie.* nupkgs on a NuGet feed
# (PackageReference when ../kithara/libs is absent).
#
# Vite assets are built during `dotnet publish` (NpmBuild target in Plume.csproj).
# META-OPS-002: Alpine final (busybox wget healthcheck — no curl).
# Build on Debian SDK so Grpc.Tools protoc (glibc) runs; publish for linux-musl-x64.
#
# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Node for Vite (NpmBuild runs on publish).
COPY --from=node:22-bookworm /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=node:22-bookworm /usr/local/bin/node /usr/local/bin/node
RUN ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

WORKDIR /src

COPY kithara/Directory.Build.props kithara/Directory.Packages.props kithara/
COPY kithara/libs/Bardie.Contracts kithara/libs/Bardie.Contracts/
COPY kithara/libs/Bardie.Module.Channel kithara/libs/Bardie.Module.Channel/
COPY kithara/libs/Bardie.Module.Hosting kithara/libs/Bardie.Module.Hosting/

COPY plume/Directory.Build.props plume/Directory.Packages.props plume/
COPY plume/Plume.csproj plume/
RUN dotnet restore plume/Plume.csproj -r linux-musl-x64

# Selective copy — parent build context includes sibling repos and local node_modules.
COPY plume/package.json plume/package-lock.json plume/vite.config.js plume/
COPY plume/assets/ plume/assets/
COPY plume/Pages/ plume/Pages/
COPY plume/Features/ plume/Features/
COPY plume/Properties/ plume/Properties/
COPY plume/wwwroot/ plume/wwwroot/
COPY plume/Program.cs plume/appsettings.json plume/appsettings.Development.json plume/
COPY plume/module.manifest.json plume/

# Re-restore after source COPY (csproj-only restore assets are incomplete for full tree).
RUN dotnet publish plume/Plume.csproj \
      -c Release -r linux-musl-x64 --self-contained false \
      -o /app/publish

# Pin alpine3.22 with Kithara/Magpie (floating `10.0-alpine` → 3.23+).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.22 AS final
WORKDIR /app

# su-exec: root entrypoint chowns volumes then drops to APP_UID (Alpine-friendly).
RUN apk add --no-cache su-exec \
    && mkdir -p /data/mtls /app/dp-keys \
    && chown -R "$APP_UID":"$APP_UID" /data /app

COPY --from=build /app/publish .
COPY plume/docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh \
    && chown -R "$APP_UID":"$APP_UID" /app

# Entrypoint starts as root to chown DP keys + mtls volumes, then drops to APP_UID.
# Empty ASPNETCORE_URLS so ModuleHosting Kestrel listeners (HTTP + work gRPC) win.
ENV ASPNETCORE_URLS= \
    BARDIE_DP_KEYS_PATH=/app/dp-keys \
    MODULE_TLS_DATA_PATH=/data/mtls \
    MODULE_WORK_GRPC_PORT=5001

EXPOSE 8080 5001
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1

ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["dotnet", "Plume.dll"]
