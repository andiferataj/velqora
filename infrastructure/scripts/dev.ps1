[CmdletBinding()]
param([ValidateSet('api','worker','web','infra')][string]$Target='infra')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/dotnet-env.ps1"
Set-Location -LiteralPath $projectRoot
if (-not (Test-Path -LiteralPath '.env')) { & "$PSScriptRoot/bootstrap.ps1" }
$values=@{}
foreach($line in Get-Content -LiteralPath '.env'){
  if($line -match '^([A-Z_]+)=(.+)$'){$values[$Matches[1]]=$Matches[2]}
}
$postgresPort=if($values.POSTGRES_PORT){$values.POSTGRES_PORT}else{'55432'}
$env:ConnectionStrings__Postgres="Host=localhost;Port=$postgresPort;Database=velqora;Username=velqora;Password=$($values.POSTGRES_PASSWORD)"
$env:RabbitMq="amqp://velqora:$($values.RABBITMQ_PASSWORD)@localhost:5672/"
$env:Redis="localhost:6379,password=$($values.REDIS_PASSWORD),abortConnect=false"
$env:DemoPassword=$values.DEMO_PASSWORD
$env:IngestionApiKey=$values.INGESTION_API_KEY
$env:ASPNETCORE_ENVIRONMENT='Development'
switch($Target){
 'infra'{docker compose up -d --wait postgres rabbitmq redis}
 'api'{$env:ASPNETCORE_URLS='http://localhost:8080';$env:DataProtectionPath=Join-Path $projectRoot '.local/keys';dotnet run --project src/backend/Velqora.Api --disable-build-servers -p:BuildInParallel=false -p:UseSharedCompilation=false}
 'worker'{$env:ASPNETCORE_URLS='http://localhost:8081';dotnet run --project src/backend/Velqora.Worker --disable-build-servers -p:BuildInParallel=false -p:UseSharedCompilation=false}
 'web'{npm run dev}
}
if($LASTEXITCODE -ne 0){throw "$Target failed."}
