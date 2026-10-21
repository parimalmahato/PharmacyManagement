[CmdletBinding()]
param(
    [switch]$LocalSqlExpress,
    [string]$Filter
)

$ErrorActionPreference = 'Stop'
$previousConnection = $env:PHARMACY_TEST_SQLSERVER
$testRoot = Split-Path -Parent $PSScriptRoot

try {
    if ($LocalSqlExpress) {
        if ($env:OS -ne 'Windows_NT') { throw 'LocalSqlExpress requires Windows.' }
        $service = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction Stop
        if ($service.Status -ne 'Running') {
            throw 'SQL Server (SQLEXPRESS) is stopped. Start that test instance, then retry.'
        }
        # Explicit local shared memory avoids named-instance network discovery.
        # No SQL Server configuration or service state is changed.
        $env:PHARMACY_TEST_SQLSERVER = 'Server=lpc:.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;'
    }
    if ([string]::IsNullOrWhiteSpace($env:PHARMACY_TEST_SQLSERVER)) {
        throw 'Set PHARMACY_TEST_SQLSERVER in this terminal, or use -LocalSqlExpress for the local disposable test server.'
    }

    Push-Location $testRoot
    try {
        $testArguments = @(
            'test', 'backend/Pharmacy.Tests/Pharmacy.Tests.csproj',
            '-c', 'ReturnValidation', '--disable-build-servers', '-m:1',
            '-p:SqlClientUseManagedNetworking=false'
        )
        if ($Filter) { $testArguments += @('--filter', $Filter) }
        & dotnet @testArguments
        if ($LASTEXITCODE -ne 0) { throw 'SQL tests failed. See backend/Pharmacy.Tests/SQL_TESTING.md.' }
    } finally { Pop-Location }
} finally {
    $env:PHARMACY_TEST_SQLSERVER = $previousConnection
}
