[CmdletBinding()]
param([switch]$Start, [switch]$Monitoring)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $projectRoot
$envFile = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envFile)) {
  function New-Secret { $bytes=New-Object byte[] 24; $rng=[Security.Cryptography.RandomNumberGenerator]::Create(); try { $rng.GetBytes($bytes); [BitConverter]::ToString($bytes).Replace('-','') } finally { $rng.Dispose() } }
  $values = @(
    "POSTGRES_PASSWORD=$(New-Secret)"
    "RABBITMQ_PASSWORD=$(New-Secret)"
    "REDIS_PASSWORD=$(New-Secret)"
    "DEMO_PASSWORD=Vq!$(New-Secret)a"
    "INGESTION_API_KEY=$(New-Secret)"
    "GRAFANA_PASSWORD=Vq!$(New-Secret)a"
  )
  [IO.File]::WriteAllLines($envFile, $values, [Text.UTF8Encoding]::new($false))
  Write-Host 'Created local credentials in .env. Keep this file private.'
} else {
  Write-Host 'Using existing .env credentials.'
}
if ($Start) {
  docker version --format '{{.Server.Version}}'
  if ($LASTEXITCODE -ne 0) { throw 'Docker engine is unavailable. Start Docker Desktop using Linux containers, then rerun this script.' }
  if ($Monitoring) { docker compose --profile monitoring up --build -d --wait --wait-timeout 300 }
  else { docker compose up --build -d --wait --wait-timeout 300 }
  if ($LASTEXITCODE -ne 0) { throw 'Startup failed. Inspect docker compose logs --tail 100.' }
  Write-Host 'Dashboard: http://localhost:4200'
  Write-Host 'Accounts: admin@velqora.local / analyst@velqora.local / viewer@velqora.local'
  Write-Host 'Read DEMO_PASSWORD from .env to sign in.'
}
