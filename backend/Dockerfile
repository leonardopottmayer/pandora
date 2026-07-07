# syntax=docker/dockerfile:1
# Build context is the repository root (needs both ./backend and ./VERSION).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Keep the repo layout so backend/Directory.Build.props can read ../VERSION.
COPY VERSION ./VERSION
COPY backend/ ./backend/

WORKDIR /src/backend

# Authenticate to the private GitHub Packages feed for the restore.
# Pass the token as a build secret:
#   docker build --secret id=github_token,env=GITHUB_TOKEN ...
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username leonardopottmayer \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text \
      --configfile nuget.config \
    && dotnet restore src/Host/Pottmayer.Pandora.Host/Pottmayer.Pandora.Host.csproj \
      --configfile nuget.config

RUN dotnet publish src/Host/Pottmayer.Pandora.Host/Pottmayer.Pandora.Host.csproj \
      -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Pottmayer.Pandora.Host.dll"]
