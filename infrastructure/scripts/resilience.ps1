[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$values=@{}
foreach($line in Get-Content -LiteralPath '.env'){if($line -match '^([A-Z_]+)=(.+)$'){$values[$Matches[1]]=$Matches[2]}}
$origin='http://localhost:4200'
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$csrf=Invoke-RestMethod "$origin/api/auth/csrf" -WebSession $session
Invoke-RestMethod "$origin/api/auth/login" -Method Post -WebSession $session -Headers @{'X-XSRF-TOKEN'=$csrf.token} -ContentType application/json -Body (@{email='admin@velqora.local';password=$values.DEMO_PASSWORD}|ConvertTo-Json)|Out-Null
function New-ResilienceEvents([int]$Count,[string]$Source){
 @(1..$Count|ForEach-Object{@{id=[Guid]::NewGuid().ToString();schemaVersion=1;occurredAt=[DateTimeOffset]::UtcNow.ToString('O');source=$Source;type='test.telemetry';device='RESILIENCE';account='test';ipAddress='192.0.2.88';synthetic=$true;attributes=@{}}})
}
function Submit-ResilienceEvents($Events){
 Invoke-RestMethod "$origin/api/ingest/events" -Method Post -ContentType application/json -Headers @{'X-Ingestion-Key'=$values.INGESTION_API_KEY} -Body (ConvertTo-Json -InputObject @($Events) -Depth 5)
}
function Wait-ResilienceDrain([int]$Expected){
 $deadline=[DateTimeOffset]::UtcNow.AddSeconds(150)
 do{
  $health=Invoke-RestMethod "$origin/api/platform" -WebSession $session
  if($health.outboxPending -eq 0 -and $health.broker.pending -eq 0 -and $health.processedEvents -ge $Expected -and $health.worker.ready){return}
  Start-Sleep -Seconds 2
 }while([DateTimeOffset]::UtcNow -lt $deadline)
 throw 'Pipeline did not durably process the expected events before the deadline.'
}
$initial=Invoke-RestMethod "$origin/api/platform" -WebSession $session
if($initial.outboxPending -ne 0 -or $initial.broker.pending -ne 0){throw 'Drain the existing backlog before running recovery acceptance.'}
$events=New-ResilienceEvents 20 'resilience-broker'
try{
 docker compose stop rabbitmq
 if($LASTEXITCODE -ne 0){throw 'Could not stop broker.'}
 if((Submit-ResilienceEvents $events).accepted -ne 20){throw 'Ingestion did not durably accept all events while broker was down.'}
 docker compose start rabbitmq
 if($LASTEXITCODE -ne 0){throw 'Could not restart broker.'}
 docker compose restart worker
 if($LASTEXITCODE -ne 0){throw 'Could not restart worker.'}
 Wait-ResilienceDrain ($initial.processedEvents+20)
 foreach($event in $events){
  $stored=Invoke-RestMethod "$origin/api/events/$($event.id)" -WebSession $session
  if($stored.id -ne $event.id){throw 'An event is missing after recovery.'}
 }
 if((Submit-ResilienceEvents $events).duplicates -ne 20){throw 'Idempotency failed after restart.'}
 $beforeCache=Invoke-RestMethod "$origin/api/platform" -WebSession $session
 docker compose stop redis
 if($LASTEXITCODE -ne 0){throw 'Could not stop cache.'}
 $cacheEvents=New-ResilienceEvents 3 'resilience-cache'
 if((Submit-ResilienceEvents $cacheEvents).accepted -ne 3){throw 'Ingestion failed during cache outage.'}
 # Detection commits before updating rebuildable Redis state. Prove processing continues while cache is down.
 $deadline=[DateTimeOffset]::UtcNow.AddSeconds(90)
 do{
  $during=Invoke-RestMethod "$origin/api/platform" -WebSession $session
  if(-not $during.redis -and $during.processedEvents -ge $beforeCache.processedEvents+3){break}
  Start-Sleep -Seconds 2
 }while([DateTimeOffset]::UtcNow -lt $deadline)
 if($during.redis -or $during.processedEvents -lt $beforeCache.processedEvents+3){throw 'Durable detection did not continue during cache outage.'}
 docker compose start redis
 if($LASTEXITCODE -ne 0){throw 'Could not restart cache.'}
 Wait-ResilienceDrain ($beforeCache.processedEvents+3)
 Write-Host 'PASS: broker outage, worker restart, retained events, duplicate ingestion and durable processing during Redis outage.'
}finally{
 docker compose start rabbitmq redis | Out-Null
 if($LASTEXITCODE -ne 0){Write-Warning 'Recovery cleanup could not restart broker/cache; inspect Docker Desktop.'}
}
