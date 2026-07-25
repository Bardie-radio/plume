# Build from the parent folder that contains `plume/` (sibling of kithara/ etc.):
#
#   docker build -f plume/Dockerfile -t plume .
#
# Vite assets are built during `dotnet publish` (NpmBuild target in Plume.csproj).

# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Node for Vite (NpmBuild runs on publish). Copy from official image — no apt NodeSource.
COPY --from=node:22-bookworm /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=node:22-bookworm /usr/local/bin/node /usr/local/bin/node
RUN ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

WORKDIR /src

COPY plume/Plume.csproj plume/
RUN dotnet restore plume/Plume.csproj

# Selective copy — parent build context includes sibling repos and local node_modules.
COPY plume/package.json plume/package-lock.json plume/vite.config.js plume/
COPY plume/assets/ plume/assets/
COPY plume/Pages/ plume/Pages/
COPY plume/Features/ plume/Features/
COPY plume/Properties/ plume/Properties/
COPY plume/wwwroot/ plume/wwwroot/
COPY plume/Program.cs plume/appsettings.json plume/appsettings.Development.json plume/

RUN dotnet publish plume/Plume.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY plume/docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh \
    && mkdir -p /app/dp-keys \
    && chown -R "$APP_UID":"$APP_UID" /app

# Entrypoint starts as root to chown the DP keys volume, then drops to APP_UID.
ENV ASPNETCORE_URLS=http://+:8080
ENV BARDIE_DP_KEYS_PATH=/app/dp-keys

EXPOSE 8080
# Prefer a static asset — /login calls Kithara discovery on every probe.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/favicon.ico || exit 1

ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["dotnet", "Plume.dll"]
