param([string]$AdminEmail = 'admin@pharmacy.local')
$ErrorActionPreference = 'Stop'
$dockerEnvPath = Join-Path (Split-Path -Parent $PSScriptRoot) '.env'
if (Test-Path -LiteralPath $dockerEnvPath) { throw '.env already exists. Keep its credentials or edit it manually; this script never overwrites them.' }
if ($AdminEmail -notmatch '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$') { throw 'Provide a valid administrator email address.' }
function New-DockerSecret {
    $secretBytes = New-Object byte[] 32
    $secretRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $secretRandom.GetBytes($secretBytes) } finally { $secretRandom.Dispose() }
    return [Convert]::ToBase64String($secretBytes)
}
$dockerSqlPassword = 'Aa1!' + (New-DockerSecret)
$dockerJwtKey = New-DockerSecret
$dockerAdminPassword = 'Aa1!' + (New-DockerSecret)
$dockerEnvText = @"
PHARMACY_SQL_PASSWORD=$dockerSqlPassword
PHARMACY_JWT_KEY=$dockerJwtKey
PHARMACY_WEB_PORT=8080
PHARMACY_BOOTSTRAP_IDENTITY=true
PHARMACY_ADMIN_EMAIL=$AdminEmail
PHARMACY_ADMIN_PASSWORD=$dockerAdminPassword
PHARMACY_ADMIN_NAME=Pharmacy Administrator
PHARMACY_AI_ENABLED=false
PHARMACY_AI_MODEL=llama3.2:3b
PHARMACY_OLLAMA_URL=http://ollama:11434/
PHARMACY_VISION_MODEL=
PHARMACY_EMBEDDING_MODEL=
PHARMACY_VECTOR_ENABLED=false
"@
# CreateNew also prevents accidental replacement if another initializer runs concurrently.
$dockerEnvStream = [System.IO.File]::Open($dockerEnvPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
try {
    $dockerEnvBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($dockerEnvText)
    $dockerEnvStream.Write($dockerEnvBytes, 0, $dockerEnvBytes.Length)
} finally { $dockerEnvStream.Dispose() }
Write-Output 'Created ignored .env with unique SQL, JWT and temporary administrator credentials.'
Write-Output "Administrator email: $AdminEmail"
Write-Output 'Read the temporary administrator password privately from .env. It must be changed at first sign-in.'
Write-Output 'Next: start Docker Desktop in Linux container mode, then run docker compose up --build -d.'
