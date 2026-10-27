[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. "$PSScriptRoot/dotnet-env.ps1"
Set-Location -LiteralPath $projectRoot
$values=@{}
foreach($line in Get-Content -LiteralPath '.env'){if($line -match '^([A-Z_]+)=(.+)$'){$values[$Matches[1]]=$Matches[2]}}
$port=if($values.POSTGRES_PORT){$values.POSTGRES_PORT}else{'55432'}
$names=@('VELQORA_TEST_POSTGRES','VELQORA_TEST_RABBITMQ','VELQORA_TEST_REDIS')
$previous=@{};foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name)}
try{
 $env:VELQORA_TEST_POSTGRES="Host=localhost;Port=$port;Database=postgres;Username=velqora;Password=$($values.POSTGRES_PASSWORD)"
 $env:VELQORA_TEST_RABBITMQ="amqp://velqora:$($values.RABBITMQ_PASSWORD)@localhost:5672/"
 $env:VELQORA_TEST_REDIS="localhost:6379,password=$($values.REDIS_PASSWORD),abortConnect=false"
 dotnet test tests/Velqora.IntegrationTests -c Release --no-build --no-restore -m:1
 if($LASTEXITCODE -ne 0){throw 'Real-service integration failed.'}
}finally{foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name])}}
