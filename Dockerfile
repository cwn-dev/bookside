# syntax=docker/dockerfile:1
# Bookside web host. Build from the repository root: docker compose build (see compose.yaml / README).

# The SDK matches global.json and runs on the build machine's own architecture; it cross-publishes for the target.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.203-noble AS build
ARG TARGETARCH
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/

# NuGet packages are cached between builds (BuildKit cache mount), so a code change doesn't re-download them.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Bookside.Web/Bookside.Web.csproj -c Release -a $TARGETARCH -o /app -p:UseAppHost=false

# Empty /data owned by the app user, so the container also starts without a mounted volume.
RUN mkdir /data

# Ubuntu chiseled: no shell or package manager, non-root `app` user (UID 1654). "extra" adds ICU and tzdata (for TZ).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app .
COPY --from=build --chown=$APP_UID:$APP_UID /data /data
USER $APP_UID

# Database and Data Protection keys live in /data (appsettings.json). Listens on HTTP 8080 (the image default).
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bookside.Web.dll"]
