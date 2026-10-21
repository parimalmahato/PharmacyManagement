param([switch]$IncludeSqlIntegration)
$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path -Parent $PSScriptRoot
Push-Location $releaseRoot
try {
    dotnet restore PharmacyManagement.sln
    if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }
    dotnet build backend/Pharmacy.Api/Pharmacy.Api.csproj --no-restore --disable-build-servers -m:1 -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    if ($IncludeSqlIntegration) {
        if (-not $env:PHARMACY_TEST_SQLSERVER) { throw 'Configure PHARMACY_TEST_SQLSERVER with a non-production test server first.' }
        dotnet test backend/Pharmacy.Tests/Pharmacy.Tests.csproj --no-restore --disable-build-servers -m:1 -c Release
    } else {
        dotnet test backend/Pharmacy.Tests/Pharmacy.Tests.csproj --no-restore --disable-build-servers -m:1 -c Release --filter 'FullyQualifiedName!~IntegrationTests'
    }
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    Push-Location frontend
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'Frontend install failed.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
        npm run lint
        if ($LASTEXITCODE -ne 0) { throw 'Frontend lint failed.' }
    } finally { Pop-Location }
} finally { Pop-Location }
