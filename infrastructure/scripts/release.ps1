[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$releaseRoot=Join-Path (Get-Location) '.local/release'
[IO.Directory]::CreateDirectory($releaseRoot)|Out-Null
$results=[Collections.Generic.List[object]]::new()
$report=Join-Path $releaseRoot 'release-results.json'
function Invoke-ReleaseGate([string]$Name,[scriptblock]$Action){
 Write-Host "Checking: $Name"
 $started=[DateTimeOffset]::UtcNow
 try{& $Action;$results.Add([pscustomobject]@{gate=$Name;passed=$true;seconds=([DateTimeOffset]::UtcNow-$started).TotalSeconds})}
 catch{$results.Add([pscustomobject]@{gate=$Name;passed=$false;error=$_.Exception.Message});throw}
 finally{@{at=[DateTimeOffset]::UtcNow.ToString('O');passed=($results.Count -eq 7 -and -not ($results|Where-Object{-not $_.passed}));gates=$results.ToArray()}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $report -Encoding utf8}
}
Invoke-ReleaseGate 'Docker access and monitored production stack' {
 docker version --format '{{.Server.Version}}'
 if($LASTEXITCODE -ne 0){throw 'Run this command in your regular PowerShell with Docker Desktop access.'}
 & "$PSScriptRoot/bootstrap.ps1" -Start -Monitoring
}
Invoke-ReleaseGate 'Builds, unit/model, isolated integration, production CSP and browser workflows' {
 & "$PSScriptRoot/verify.ps1" -UiSmoke -E2E
}
Invoke-ReleaseGate 'Dependency audit' {
 npm audit --audit-level=high
 if($LASTEXITCODE -ne 0){throw 'npm dependency audit failed.'}
 . "$PSScriptRoot/dotnet-env.ps1"
 dotnet restore Velqora.slnx --locked-mode --disable-build-servers --disable-parallel -m:1 -p:NuGetAuditMode=all -p:NuGetAuditLevel=low
 if($LASTEXITCODE -ne 0){throw 'NuGet lock/audit validation failed.'}
}
foreach($line in Get-Content -LiteralPath '.env'){
 if($line -match '^DEMO_PASSWORD=(.+)$'){$env:VELQORA_DEMO_PASSWORD=$Matches[1]}
 if($line -match '^INGESTION_API_KEY=(.+)$'){$env:VELQORA_INGESTION_KEY=$Matches[1]}
}
Invoke-ReleaseGate '100 events/sec ingestion and durable drain' {
 node tools/benchmark.cjs
 if($LASTEXITCODE -ne 0){throw 'Performance acceptance failed.'}
}
Invoke-ReleaseGate 'Broker/cache outage and worker restart recovery' { & "$PSScriptRoot/resilience.ps1" }
Invoke-ReleaseGate 'Backup restore into a disposable database' { & "$PSScriptRoot/backup-rehearsal.ps1" }
Invoke-ReleaseGate 'Exported metrics, monitoring health and distributed trace continuation' {
 node tools/check-monitoring.cjs
 if($LASTEXITCODE -ne 0){throw 'Monitoring acceptance failed.'}
}
docker compose images --format json | Set-Content -LiteralPath (Join-Path $releaseRoot 'images.json') -Encoding utf8
docker stats --no-stream --format '{{json .}}' | Set-Content -LiteralPath (Join-Path $releaseRoot 'resources.jsonl') -Encoding utf8
Write-Host "PASS: all seven local release gates. Evidence: $report"
Write-Host 'Dashboard: http://localhost:4200'
