[CmdletBinding()]
param([switch]$Offline, [switch]$WithoutContainers, [switch]$UiSmoke, [switch]$E2E)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$env:DOTNET_CLI_HOME=Join-Path (Get-Location) '.local/dotnet'
$env:NUGET_HTTP_CACHE_PATH=Join-Path (Get-Location) '.local/nuget-http-cache'
$env:NUGET_SCRATCH=Join-Path (Get-Location) '.local/nuget-scratch'
$env:NUGET_PLUGINS_CACHE_PATH=Join-Path (Get-Location) '.local/nuget-plugins'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:PLAYWRIGHT_BROWSERS_PATH=Join-Path (Get-Location) '.local/browsers'
if($WithoutContainers -and $E2E){throw '-E2E requires the running Compose application; omit -WithoutContainers.'}
dotnet run --project tests/Velqora.CoreChecks --disable-build-servers -p:BuildInParallel=false -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Core specification checks failed.'}
dotnet run --project tools/SourceChecks --disable-build-servers -p:BuildInParallel=false -p:UseSharedCompilation=false -- (Get-Location).Path
if($LASTEXITCODE -ne 0){throw 'Source checks failed.'}
docker compose --env-file .env.example config --quiet
if($LASTEXITCODE -ne 0){throw 'Compose configuration is invalid.'}
if($Offline){return}
dotnet restore Velqora.slnx --disable-build-servers --disable-parallel -m:1
if($LASTEXITCODE -ne 0){throw 'Restore failed.'}
dotnet build Velqora.slnx -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Backend build failed.'}
dotnet test tests/Velqora.UnitTests -c Release --no-build --no-restore -m:1
if($LASTEXITCODE -ne 0){throw 'Unit tests failed.'}
dotnet build tools/ModelChecks -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Model check build failed.'}
dotnet tools/ModelChecks/bin/Release/net10.0/ModelChecks.dll (Get-Location).Path
if($LASTEXITCODE -ne 0){throw 'EF model validation failed.'}
npm ci --no-audit --no-fund --cache .local/npm-cache
if($LASTEXITCODE -ne 0){throw 'Frontend dependency installation failed.'}
npm --prefix src/frontend/velqora-dashboard run build -- --output-path ../../../.local/dashboard-build
if($LASTEXITCODE -ne 0){throw 'Angular build failed.'}
if(-not $WithoutContainers){
  dotnet test tests/Velqora.IntegrationTests -c Release --no-build --no-restore -m:1
  if($LASTEXITCODE -ne 0){throw 'Integration tests failed.'}
}
if($UiSmoke -or $E2E){
  npx playwright install chromium
  if($LASTEXITCODE -ne 0){throw 'Browser installation failed.'}
}
if($UiSmoke){
  node tools/smoke-dashboard.cjs
  if($LASTEXITCODE -ne 0){throw 'Static UI smoke check failed.'}
}
if($E2E){
  foreach($line in Get-Content -LiteralPath '.env'){
    if($line -match '^DEMO_PASSWORD=(.+)$'){$env:VELQORA_DEMO_PASSWORD=$Matches[1]}
    if($line -match '^INGESTION_API_KEY=(.+)$'){$env:VELQORA_INGESTION_KEY=$Matches[1]}
  }
  npm run test:e2e
  if($LASTEXITCODE -ne 0){throw 'End-to-end tests failed.'}
}
if($WithoutContainers){Write-Host 'PASS: build/unit/model checks. Container integration was explicitly excluded.'}
