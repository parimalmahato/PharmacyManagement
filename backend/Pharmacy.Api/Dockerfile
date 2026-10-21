# Build from the repository root: docker build -f backend/Pharmacy.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS source
WORKDIR /src
COPY backend/Pharmacy.Domain/Pharmacy.Domain.csproj backend/Pharmacy.Domain/
COPY backend/Pharmacy.Application/Pharmacy.Application.csproj backend/Pharmacy.Application/
COPY backend/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj backend/Pharmacy.Infrastructure/
COPY backend/Pharmacy.Api/Pharmacy.Api.csproj backend/Pharmacy.Api/
COPY backend/Pharmacy.Migrator/Pharmacy.Migrator.csproj backend/Pharmacy.Migrator/
RUN dotnet restore backend/Pharmacy.Api/Pharmacy.Api.csproj \
    && dotnet restore backend/Pharmacy.Migrator/Pharmacy.Migrator.csproj
COPY backend/ backend/
# Infrastructure embeds this guide; preserve its relative location at publish time.
COPY ApplicationHelp/USER_GUIDE.md ApplicationHelp/USER_GUIDE.md

FROM source AS api-build
RUN dotnet publish backend/Pharmacy.Api/Pharmacy.Api.csproj -c Release --no-restore \
    -o /out/api /p:UseAppHost=false

FROM source AS migration-build
RUN dotnet publish backend/Pharmacy.Migrator/Pharmacy.Migrator.csproj -c Release --no-restore \
    -o /out/migrator /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    PHARMACY_REPORT_FONT=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf
USER $APP_UID

# One-shot migration image: Compose waits for successful completion before API startup.
FROM runtime AS migrator
COPY --from=migration-build /out/migrator/ ./
ENTRYPOINT ["dotnet", "Pharmacy.Migrator.dll"]

FROM runtime AS final
COPY --from=api-build /out/api/ ./
EXPOSE 8080
HEALTHCHECK --interval=20s --timeout=5s --start-period=40s --retries=5 \
    CMD curl --fail --silent http://127.0.0.1:8080/api/health/ready || exit 1
ENTRYPOINT ["dotnet", "Pharmacy.Api.dll"]
