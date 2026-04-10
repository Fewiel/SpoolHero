FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY SpoolManager.sln ./
RUN echo '{"sdk":{"version":"10.0.100-preview.7","rollForward":"latestPatch","allowPrerelease":true}}' > global.json
COPY src/SpoolManager.Shared/SpoolManager.Shared.csproj src/SpoolManager.Shared/
COPY src/SpoolManager.Infrastructure/SpoolManager.Infrastructure.csproj src/SpoolManager.Infrastructure/
COPY src/SpoolManager.Client/SpoolManager.Client.csproj src/SpoolManager.Client/
COPY src/SpoolManager.Server/SpoolManager.Server.csproj src/SpoolManager.Server/
RUN dotnet restore

COPY src/ src/
RUN dotnet publish src/SpoolManager.Server/SpoolManager.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/api/public/branding || exit 1

ENTRYPOINT ["dotnet", "SpoolManager.Server.dll"]
