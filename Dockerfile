# Built by GitHub Actions (`flyctl deploy --remote-only`), never on a dev
# machine. The Mac is ARM64 and Fly runs x86-64 — see README.md.

# ---------------------------------------------------------------
# Build
# ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer so a source-only change does not re-download
# the whole NuGet graph. global.json pins the SDK feature band.
COPY global.json ./
COPY Directory.Packages.props Directory.Build.props* ./
COPY src/Dibal.Domain/Dibal.Domain.csproj                 src/Dibal.Domain/
COPY src/Dibal.Infrastructure/Dibal.Infrastructure.csproj src/Dibal.Infrastructure/
COPY src/Dibal.Web/Dibal.Web.csproj                       src/Dibal.Web/
RUN dotnet restore src/Dibal.Web/Dibal.Web.csproj

COPY . .

# Tailwind runs as part of Build via src/Dibal.Web/Tailwind.targets.
RUN dotnet publish src/Dibal.Web/Dibal.Web.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Must match ASPNETCORE_URLS and http_service.internal_port in fly.toml.
EXPOSE 8080

# Data Protection keys are persisted to the Fly volume mounted at /keys.
# Without this the SMTP password becomes unreadable on every deploy and
# every user is signed out — see the [mounts] block in fly.toml.
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# The aspnet image ships a non-root `app` user (uid 1654). /keys is a mount
# point at runtime, so Fly owns its permissions; nothing to chown here.
USER app

ENTRYPOINT ["dotnet", "Dibal.Web.dll"]
